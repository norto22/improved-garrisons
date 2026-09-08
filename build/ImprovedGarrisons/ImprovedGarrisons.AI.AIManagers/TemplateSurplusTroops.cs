using System;
using System.Collections.Generic;
using System.Linq;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace ImprovedGarrisons.AI.AIManagers
{
	internal static class TemplateSurplusTroops
	{
		public static Dictionary<CharacterObject, int> GetSurplusTroops(TroopRoster garrison, IDictionary<CharacterObject, int> template)
		{
			Dictionary<CharacterObject, int> surplus = new Dictionary<CharacterObject, int>();
			if (garrison == null || template == null || template.Count == 0)
			{
				return surplus;
			}

			List<TroopRosterElement> troops = garrison.GetTroopRoster()
				.Where(x => x.Character != null && !x.Character.IsHero && x.Number > 0)
				.OrderByDescending(x => x.Character.Level).ThenBy(x => x.Character.StringId, StringComparer.Ordinal).ToList();
			List<KeyValuePair<CharacterObject, int>> targets = template.Where(x => x.Key != null).ToList();
			if (targets.Count == 0)
			{
				return surplus;
			}
			int[] available = troops.Select(x => x.Number).ToArray();
			int[] needed = targets.Select(x => Math.Max(0, x.Value)).ToArray();
			List<HashSet<CharacterObject>> reachable = troops.Select(x => GetReachableTargets(x.Character)).ToList();

			// Keep completed targets first. A zero target means unlimited in existing templates.
			for (int i = 0; i < troops.Count; i++)
			{
				for (int j = 0; j < targets.Count; j++)
				{
					if (troops[i].Character == targets[j].Key && targets[j].Value > 0)
					{
						int reserved = Math.Min(available[i], needed[j]);
						available[i] -= reserved;
						needed[j] -= reserved;
					}
				}
			}
			for (int i = 0; i < troops.Count; i++)
			{
				if (targets.Any(target => target.Value <= 0 && reachable[i].Contains(target.Key)))
				{
					available[i] = 0;
				}
			}

			// Capacitated matching lets a shared recruit be reassigned to another upgrade branch.
			// Each troop can occupy only one target slot, including when paths merge or split.
			int source = troops.Count + targets.Count;
			int sink = source + 1;
			int[,] capacity = new int[sink + 1, sink + 1];
			for (int i = 0; i < troops.Count; i++)
			{
				capacity[source, i] = available[i];
				for (int j = 0; j < targets.Count; j++)
				{
					if (needed[j] > 0 && reachable[i].Contains(targets[j].Key))
					{
						capacity[i, troops.Count + j] = available[i];
					}
				}
			}
			for (int j = 0; j < targets.Count; j++)
			{
				capacity[troops.Count + j, sink] = needed[j];
			}
			ReserveReachableTroops(capacity, source, sink);
			for (int i = 0; i < troops.Count; i++)
			{
				int count = Math.Min(capacity[source, i], troops[i].Number - troops[i].WoundedNumber);
				if (count > 0)
				{
					surplus.Add(troops[i].Character, count);
				}
			}
			return surplus;
		}

		public static bool TryCreateGuard(TroopRoster garrison, GarrisonSettings settings, bool canCreate, Func<TroopRoster> createGuard)
		{
			if (settings == null || !settings.GuardsAutoSpawnFromExcess || !canCreate || createGuard == null
				|| garrison == null || settings.GuardsAutoSpawnSize <= 0 || settings.Template == null)
			{
				return false;
			}
			Dictionary<CharacterObject, int> surplus = GetSurplusTroops(garrison, settings.Template.GetTroopListAsCharacterObjects());
			int size = settings.GuardsAutoSpawnSize;
			if (surplus.Values.Sum() < size)
			{
				return false;
			}
			Dictionary<CharacterObject, int> selected = new Dictionary<CharacterObject, int>();
			foreach (KeyValuePair<CharacterObject, int> troop in surplus)
			{
				int count = Math.Min(troop.Value, size);
				selected.Add(troop.Key, count);
				size -= count;
				if (size == 0)
				{
					break;
				}
			}

			// Native party creation must succeed before removing any garrison troops.
			TroopRoster guards = createGuard();
			if (guards == null || guards == garrison || guards.TotalManCount != 0)
			{
				return false;
			}
			Dictionary<CharacterObject, int> currentSurplus = GetSurplusTroops(garrison, settings.Template.GetTroopListAsCharacterObjects());
			if (selected.Any(x => !currentSurplus.TryGetValue(x.Key, out int count) || count < x.Value))
			{
				return false;
			}
			List<TroopRosterElement> beforeGarrison = garrison.GetTroopRoster().ToList();
			List<TroopRosterElement> beforeGuards = guards.GetTroopRoster().ToList();
			try
			{
				foreach (KeyValuePair<CharacterObject, int> troop in selected)
				{
					TroopRosterElement element = garrison.GetElementCopyAtIndex(garrison.FindIndexOfTroop(troop.Key));
					int xp = (int)((long)element.Xp * troop.Value / element.Number);
					guards.AddToCounts(troop.Key, troop.Value, xpChange: xp);
					garrison.AddToCounts(troop.Key, -troop.Value, xpChange: -xp);
				}
				return true;
			}
			catch
			{
				garrison.Clear();
				foreach (TroopRosterElement troop in beforeGarrison)
				{
					garrison.Add(troop);
				}
				guards.Clear();
				foreach (TroopRosterElement troop in beforeGuards)
				{
					guards.Add(troop);
				}
				throw;
			}
		}

		private static HashSet<CharacterObject> GetReachableTargets(CharacterObject troop)
		{
			HashSet<CharacterObject> reachable = new HashSet<CharacterObject>();
			Stack<CharacterObject> pending = new Stack<CharacterObject>();
			pending.Push(troop);
			while (pending.Count > 0)
			{
				CharacterObject current = pending.Pop();
				if (current == null || !reachable.Add(current) || current.UpgradeTargets == null)
				{
					continue;
				}
				foreach (CharacterObject upgrade in current.UpgradeTargets)
				{
					pending.Push(upgrade);
				}
			}
			return reachable;
		}

		private static void ReserveReachableTroops(int[,] capacity, int source, int sink)
		{
			while (true)
			{
				int[] previous = Enumerable.Repeat(-1, sink + 1).ToArray();
				Queue<int> pending = new Queue<int>();
				previous[source] = source;
				pending.Enqueue(source);
				while (pending.Count > 0 && previous[sink] < 0)
				{
					int current = pending.Dequeue();
					for (int next = 0; next <= sink; next++)
					{
						if (previous[next] < 0 && capacity[current, next] > 0)
						{
							previous[next] = current;
							pending.Enqueue(next);
						}
					}
				}
				if (previous[sink] < 0)
				{
					return;
				}
				int count = int.MaxValue;
				for (int node = sink; node != source; node = previous[node])
				{
					count = Math.Min(count, capacity[previous[node], node]);
				}
				for (int node = sink; node != source; node = previous[node])
				{
					capacity[previous[node], node] -= count;
					capacity[node, previous[node]] += count;
				}
			}
		}
	}
}
