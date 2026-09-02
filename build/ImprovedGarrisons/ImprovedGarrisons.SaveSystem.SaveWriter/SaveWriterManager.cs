using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.FilePaths;
using ImprovedGarrisons.SaveSystem.SaveData;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem.SaveWriter
{
	public class SaveWriterManager
	{
		private GlobalSettingsFilePath globalSettingsFilePath = new GlobalSettingsFilePath();

		public bool CreateAndUpdateSaveFile(SettlementSaveFilePath name)
		{
			try
			{
				FileWriter.SerializeToBin(IGSaveData.Instance, name.CombinedPath);
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex, withoutMessage: true);
				return false;
			}
		}

		public bool CreateAndUpdateGlobalSettings()
		{
			try
			{
				FileWriter.SerializeToBin(GlobalSettings.Instance, globalSettingsFilePath.CombinedPath);
				return true;
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex, withoutMessage: true);
				return false;
			}
		}

		public bool DeleteSaveFile(IGSaveFilePath filePath)
		{
			try
			{
				return FileWriter.DeleteSaveFileByPath(filePath.CombinedPath);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex, withoutMessage: true);
				return false;
			}
		}

		public bool LoadSaveFile(IGSaveFilePath filePath)
		{
			try
			{
				if (filePath.PathIsValid())
				{
					if (File.Exists(filePath.CombinedPath))
					{
						IGSaveData.Instance = FileWriter.DeserializeFromBin<IGSaveData>(filePath.CombinedPath);
					}
					Main.GarrisonBehavior.InitializeSettlements();
					return true;
				}
				if (Campaign.Current != null && Campaign.Current.GameStarted)
				{
					InformationManager.DisplayMessage(new InformationMessage("Warning. The configuration path for Improved Garrisons could not be set. The mod will not be able to save its settings.", Color.FromUint(ModuleColors.red)));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			Main.GarrisonBehavior.InitializeSettlements();
			return false;
		}

		public bool LoadGlobalSettings()
		{
			try
			{
				if (globalSettingsFilePath.PathIsValid())
				{
					if (File.Exists(globalSettingsFilePath.CombinedPath))
					{
						GlobalSettings.Instance = FileWriter.DeserializeFromBin<GlobalSettings>(globalSettingsFilePath.CombinedPath);
						return true;
					}
				}
				else if (Campaign.Current != null && Campaign.Current.GameStarted)
				{
					InformationManager.DisplayMessage(new InformationMessage("Warning. The configuration path for Improved Garrisons could not be set. The mod will not be able to save its settings.", Color.FromUint(ModuleColors.red)));
				}
			}
			catch (SerializationException)
			{
				InformationManager.ShowInquiry(new InquiryData(new TextObject("{=misc_update_title2}Improved Garrison Update").ToString(), new TextObject("{=misc_update_desc2}Thank you for using Improved Garrisons! \n \nThere has been a major update for the mod which includes a new save system. Unfortunately this new save system is not compatible with the old way the mods settings have been saved. Therefore your mods data has been reset which includes your configuration and settlement specific settings like templates. \n \nI would recommend setting up the mods data before you continue playing. \n \nIf you want to read more about this update, head to Improved Garrisons on Nexusmods.\n \n Have fun playing! \n ~ Sidies").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, new TextObject("{=menu_okay}Okay").ToString(), null, delegate
				{
					CreateAndUpdateGlobalSettings();
				}, null));
			}
			catch (Exception ex2)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex2);
			}
			return false;
		}
	}
}
