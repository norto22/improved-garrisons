const tls = require("node:tls");
const crypto = require("node:crypto");

async function main() {
    const baseUrl = process.env.PTERO_URL;
    const apiToken = process.env.PTERO_API;
    const serverId = process.env.PTERO_SERVER_ID;
    if (!baseUrl || !apiToken || !serverId) {
        throw new Error("PTERO_URL, PTERO_API, and PTERO_SERVER_ID are required.");
    }

    const response = await fetch(`${baseUrl}/api/client/servers/${serverId}/websocket`, {
        headers: { Authorization: `Bearer ${apiToken}`, Accept: "application/json" },
    });
    if (!response.ok) {
        throw new Error(`WebSocket credentials request failed with ${response.status}.`);
    }

    const credentials = (await response.json()).data;
    const endpoint = new URL(credentials.socket);
    const key = crypto.randomBytes(16).toString("base64");
    const socket = tls.connect({
        host: endpoint.hostname,
        port: Number(endpoint.port || 443),
        servername: endpoint.hostname,
        rejectUnauthorized: true,
    });

    let handshakePending = true;
    let buffer = Buffer.alloc(0);
    let fragments = [];

    function sendFrame(opcode, payload) {
        const body = Buffer.from(payload);
        const mask = crypto.randomBytes(4);
        let header;
        if (body.length < 126) {
            header = Buffer.from([0x80 | opcode, 0x80 | body.length]);
        } else {
            header = Buffer.alloc(4);
            header[0] = 0x80 | opcode;
            header[1] = 0x80 | 126;
            header.writeUInt16BE(body.length, 2);
        }

        const masked = Buffer.alloc(body.length);
        for (let index = 0; index < body.length; index++) {
            masked[index] = body[index] ^ mask[index % 4];
        }
        socket.write(Buffer.concat([header, mask, masked]));
    }

    function sendJson(value) {
        sendFrame(1, Buffer.from(JSON.stringify(value)));
    }

    function handleText(payload) {
        const message = JSON.parse(payload.toString("utf8"));
        if (message.event === "auth success") {
            sendJson({ event: "send logs", args: [null] });
        }
        if (message.event !== "console output") {
            return;
        }

        for (const raw of message.args ?? []) {
            const line = String(raw)
                .replace(/\x1b\[[0-9;]*m/g, "")
                .replace(/ptl[ac]_[A-Za-z0-9]+/g, "[REDACTED]");
            if (line.length < 2_000 && !line.includes(":/home/container$ CFG=")) {
                process.stdout.write(`${line}\n`);
            }
        }
    }

    function consumeFrames() {
        while (buffer.length >= 2) {
            const first = buffer[0];
            const final = (first & 0x80) !== 0;
            const opcode = first & 0x0f;
            let length = buffer[1] & 0x7f;
            let offset = 2;
            if (length === 126) {
                if (buffer.length < 4) return;
                length = buffer.readUInt16BE(2);
                offset = 4;
            } else if (length === 127) {
                if (buffer.length < 10) return;
                length = Number(buffer.readBigUInt64BE(2));
                offset = 10;
            }
            if (buffer.length < offset + length) return;

            const payload = buffer.subarray(offset, offset + length);
            buffer = buffer.subarray(offset + length);
            if (opcode === 8) {
                socket.end();
                return;
            }
            if (opcode === 9) {
                sendFrame(10, payload);
                continue;
            }
            if (opcode === 1) {
                fragments = [payload];
            } else if (opcode === 0 && fragments.length > 0) {
                fragments.push(payload);
            } else {
                continue;
            }
            if (final) {
                handleText(Buffer.concat(fragments));
                fragments = [];
            }
        }
    }

    socket.on("secureConnect", () => {
        socket.write([
            `GET ${endpoint.pathname}${endpoint.search} HTTP/1.1`,
            `Host: ${endpoint.host}`,
            "Connection: Upgrade",
            "Upgrade: websocket",
            `Origin: ${baseUrl}`,
            "Sec-WebSocket-Version: 13",
            `Sec-WebSocket-Key: ${key}`,
            "\r\n",
        ].join("\r\n"));
    });

    socket.on("data", (chunk) => {
        buffer = Buffer.concat([buffer, chunk]);
        if (handshakePending) {
            const end = buffer.indexOf("\r\n\r\n");
            if (end < 0) return;
            const status = buffer.subarray(0, end).toString("utf8").split("\r\n", 1)[0];
            if (!status.includes(" 101 ")) throw new Error(status);
            buffer = buffer.subarray(end + 4);
            handshakePending = false;
            sendJson({ event: "auth", args: [credentials.token] });
        }
        consumeFrames();
    });

    socket.on("error", (error) => {
        process.stderr.write(`${error.message}\n`);
        process.exit(1);
    });

    const durationMilliseconds = Number(process.env.PTERO_CONSOLE_DURATION_MS || 10_000);
    setTimeout(() => {
        socket.end();
        process.exit(0);
    }, durationMilliseconds);
}

main().catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exit(1);
});
