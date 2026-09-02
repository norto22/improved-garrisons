using System;
using System.Collections.Generic;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.SaveSystem.SaveData.DataTypes
{
	[Serializable]
	public class GlobalSettings
	{
		private static GlobalSettings _instance = null;

		private static string _latestVersion = "c1.0.0";

		public string Version { get; set; } = _latestVersion;

		public bool DisableErrorMessage { get; set; }

		public bool EnableImprovedGarrisonsUIOnMap { get; set; }

		public static GlobalSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new GlobalSettings();
				}
				return _instance;
			}
			set
			{
				_instance = value;
			}
		}

		public Dictionary<string, TrainingTemplate> TrainingTemplates { get; set; } = new Dictionary<string, TrainingTemplate>();

		public void AddTrainingTemplate(TrainingTemplate template)
		{
			if (!TemplateNameIsAlreadyUsed(template.Name))
			{
				TrainingTemplates.Add(template.Name, template);
				SaveSystemManager.Instance.SaveGlobalSettings();
				return;
			}
			string titleText = new TextObject("{=menu_template_alreadyused}Name already used").ToString();
			string text = new TextObject("{=menu_template_alreadyused_desc}This template name is already used, Do you want to overwrite this template?").ToString();
			InformationManager.ShowInquiry(new InquiryData(titleText, text, isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=menu_yes}Yes").ToString(), new TextObject("{=menu_cancel}Cancel").ToString(), delegate
			{
				TrainingTemplates.Remove(template.Name);
				TrainingTemplates.Add(template.Name, template);
			}, null));
		}

		private bool TemplateNameIsAlreadyUsed(string name)
		{
			try
			{
				foreach (string key in TrainingTemplates.Keys)
				{
					if (key.Equals(name))
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex);
			}
			return false;
		}

		public bool VersionNumberIsUpToDate()
		{
			return Version.Equals(_latestVersion);
		}
	}
}
