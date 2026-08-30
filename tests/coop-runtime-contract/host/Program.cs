using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 2)
{
    Console.Error.WriteLine("Expected the contract worker assembly and repository root.");
    return 2;
}

string workerPath = Path.GetFullPath(args[0]);
string repositoryRoot = Path.GetFullPath(args[1]);
using CoopContractLoadContext context = new(workerPath, repositoryRoot);
Assembly worker = context.LoadFromAssemblyPath(workerPath);
Type runner = worker.GetType("ImprovedGarrisons.CoopRuntimeContract.ContractRunner", throwOnError: true)!;
MethodInfo run = runner.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
    ?? throw new MissingMethodException(runner.FullName, "Run");

try
{
    return (int)(run.Invoke(null, null) ?? 1);
}
catch (TargetInvocationException exception) when (exception.InnerException != null)
{
    Console.Error.WriteLine(exception.InnerException);
    return 1;
}

internal sealed class CoopContractLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly string[] searchDirectories;

    public CoopContractLoadContext(string workerPath, string repositoryRoot)
        : base("ImprovedGarrisons.CoopRuntimeContract", isCollectible: true)
    {
        searchDirectories = new[]
        {
            Path.GetDirectoryName(workerPath)!,
            Path.Combine(repositoryRoot, "ImprovedGarrisons", "bin", "Win64_Shipping_Client"),
            Path.Combine(repositoryRoot, "ImprovedGarrisons", "bin", "Win64_Shipping_Client", "Adapters"),
            Path.Combine(repositoryRoot, "VanillaModuleFiles", "BannerlordCoop", "bin", "Win64_Shipping_Client"),
            Path.Combine(repositoryRoot, "VanillaSourceFiles", "Win64_Shipping_Client"),
            Path.Combine(repositoryRoot, "VanillaModuleFiles", "Native", "bin", "Win64_Shipping_Client"),
            Path.Combine(repositoryRoot, "VanillaModuleFiles", "SandBox", "bin", "Win64_Shipping_Client"),
            Path.Combine(repositoryRoot, "VanillaModuleFiles", "SandBoxCore", "bin", "Win64_Shipping_Client")
        };
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "System.Private.CoreLib" or "System.Runtime" or "netstandard")
        {
            return null;
        }

        foreach (string directory in searchDirectories)
        {
            string candidate = Path.Combine(directory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
        }

        return null;
    }

    public void Dispose()
    {
        Unload();
    }
}
