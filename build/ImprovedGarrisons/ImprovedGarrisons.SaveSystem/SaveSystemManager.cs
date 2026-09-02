using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.FilePaths;
using ImprovedGarrisons.SaveSystem.SaveWriter;
using ImprovedGarrisons.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ImprovedGarrisons.SaveSystem
{
	public class SaveSystemManager
	{
		private static SaveSystemManager _instance;

		private string UniqueGameId = null;

		private SaveWriterManager saveWriterManager;

		private string _currentSaveSlotNameWithID;

		public static SaveSystemManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new SaveSystemManager();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		private SaveGameFileInfo[] CurrentMBSaveFiles => MBSaveLoad.GetSaveFiles();

		public string CurrentSaveSlotNameWithID
		{
			get
			{
				try
				{
					string activeSaveSlotName = MBSaveLoad.ActiveSaveSlotName;
					if (string.IsNullOrEmpty(activeSaveSlotName) && _currentSaveSlotNameWithID != null)
					{
						return _currentSaveSlotNameWithID;
					}
					if (string.IsNullOrEmpty(activeSaveSlotName))
					{
						string nameOfLatestSaveByID = GetNameOfLatestSaveByID(Campaign.Current.UniqueGameId);
						if (string.IsNullOrEmpty(nameOfLatestSaveByID))
						{
							SaveGameFileInfo saveGameFileInfo = MBSaveLoad.GetSaveFiles((SaveGameFileInfo info) => info.Name.StartsWith("saveauto", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
							if (saveGameFileInfo != null)
							{
								return saveGameFileInfo.Name + "_";
							}
							return null;
						}
						_currentSaveSlotNameWithID = AddUniqueGameIDToString(nameOfLatestSaveByID);
						return _currentSaveSlotNameWithID;
					}
					_currentSaveSlotNameWithID = AddUniqueGameIDToString(activeSaveSlotName);
					return _currentSaveSlotNameWithID;
				}
				catch (Exception)
				{
					LogFileManager.WriteErrorLogEntry("Could not find the current save slot name. Loading/Saving failed.");
					return null;
				}
			}
		}

		public SaveSystemManager()
		{
			saveWriterManager = new SaveWriterManager();
		}

		public string AddUniqueGameIDToString(string name)
		{
			string text = "";
			if (Campaign.Current != null)
			{
				text = Campaign.Current.UniqueGameId;
				if (text != null)
				{
					UniqueGameId = text;
				}
			}
			else
			{
				text = UniqueGameId;
			}
			if (text == null)
			{
				text = "";
			}
			return name + "_" + text;
		}

		public bool SaveSettlementSaveData(string saveGameName)
		{
			try
			{
				SettlementSaveFilePath name = new SettlementSaveFilePath(AddUniqueGameIDToString(saveGameName));
				saveWriterManager.CreateAndUpdateSaveFile(name);
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public bool SaveGlobalSettings()
		{
			try
			{
				saveWriterManager.CreateAndUpdateGlobalSettings();
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public void LoadSettlementSaveDataAndGlobalSettings(CampaignGameStarter starter)
		{
			try
			{
				SettlementSaveFilePath filePath = new SettlementSaveFilePath(CurrentSaveSlotNameWithID);
				saveWriterManager.LoadSaveFile(filePath);
				saveWriterManager.LoadGlobalSettings();
				Campaign campaign = Game.Current.GameType as Campaign;
				if (!Main.IsDedicatedServer && campaign != null && campaign.CampaignGameLoadingType != Campaign.GameLoadingType.NewCampaign && !ConfigManager.Instance.Config.VersionNumberIsUpToDate())
				{
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=misc_update_title}Improved Garrison update").ToString(), new TextObject("{=misc_update_desc}~ Thanks for using Improved Garrisons \n \nThere are new configuration options in the latest update!\n \nIt is recommended to reset the configuration to its default values. These new configurations are otherwise set to false or 0. \n \nYou can always check this mod's settings by pressing ALT + G. \n \nHave fun playing! \n~ Sidies").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, new TextObject("{=menu_okay}Okay").ToString(), null, delegate
					{
						ConfigManager.Instance.Config.UpdateVersion();
						Main.GarrisonPartyBehavior.ReturnAllIGParties();
					}, null));
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		public void DeleteUnnecessarySaveAndConfigFiles()
		{
			try
			{
				SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles();
				string saveFilesPath = new IGSaveFilePath().SaveFilesPath;
				string[] files = Directory.GetFiles(saveFilesPath);
				foreach (string item in files.ToList())
				{
					string iGSaveNameFromPath = GetIGSaveNameFromPath(item);
					string iGSaveIDFromPath = getIGSaveIDFromPath(item);
					bool flag = false;
					bool flag2 = false;
					SaveGameFileInfo[] array = saveFiles;
					foreach (SaveGameFileInfo saveGameFileInfo in array)
					{
						flag = iGSaveIDFromPath == saveGameFileInfo.MetaData.GetUniqueGameId();
						flag2 = iGSaveNameFromPath == saveGameFileInfo.Name;
						if (flag && flag2)
						{
							break;
						}
					}
					if (!flag && !flag2)
					{
						saveWriterManager.DeleteSaveFile(new SettlementSaveFilePath(iGSaveNameFromPath + "_" + iGSaveIDFromPath));
						ConfigManager.Instance.DeleteConfig(new ConfigFilePath(iGSaveNameFromPath + "_" + iGSaveIDFromPath));
						InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=info_save_delete}The Improved Garrison data has been deleted from this save:").ToString() + ModuleStrings._space + iGSaveNameFromPath, Color.FromUint(ModuleColors.modMainColor)));
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
		}

		private string GetNameOfLatestSaveByID(string id)
		{
			try
			{
				if (id != null)
				{
					string saveFilesPath = new IGSaveFilePath().SaveFilesPath;
					List<string> list = (from file in Directory.GetFiles(saveFilesPath)
						where file.Contains(id)
						select file).ToList();
					DateTime dateTime = DateTime.MinValue;
					string text = "";
					foreach (string item in list)
					{
						string text2 = Path.Combine(saveFilesPath, item);
						DateTime lastWriteTime = File.GetLastWriteTime(text2);
						if (dateTime < lastWriteTime)
						{
							dateTime = lastWriteTime;
							text = text2;
						}
					}
					if (text.Count() > 0)
					{
						return GetIGSaveNameFromPath(text);
					}
				}
			}
			catch (Exception)
			{
			}
			return null;
		}

		private string GetIGSaveNameFromPath(string path)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			int num = fileNameWithoutExtension.IndexOf('_') + 1;
			int num2 = fileNameWithoutExtension.LastIndexOf('_');
			int length = fileNameWithoutExtension.Length;
			int num3 = fileNameWithoutExtension.Count((char f) => f == '_');
			if (!fileNameWithoutExtension.StartsWith("IG") || num <= 0 || num2 < 0 || num3 < 2)
			{
				return null;
			}
			return fileNameWithoutExtension.Substring(num, num2 - num);
		}

		private string getIGSaveIDFromPath(string path)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			int num = fileNameWithoutExtension.IndexOf('_') + 1;
			int num2 = fileNameWithoutExtension.LastIndexOf('_');
			int length = fileNameWithoutExtension.Length;
			int num3 = fileNameWithoutExtension.Count((char f) => f == '_');
			if (!fileNameWithoutExtension.StartsWith("IG") || num <= 0 || num2 < 0 || num3 < 2)
			{
				return null;
			}
			return fileNameWithoutExtension.Substring(num2 + 1, length - num2 - 1);
		}
	}
}
