using System;
using System.IO;
using System.Reflection;
using ImprovedGarrisons.Debugging.LogFileSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace ImprovedGarrisons.SaveSystem.FilePaths
{
	public class IGSaveFilePath
	{
		private string _saveFilesPath;

		public string CombinedPath { get; protected set; }

		public string FileName { get; protected set; }

		public string SaveFilesPath
		{
			get
			{
				if (_saveFilesPath != null)
				{
					return _saveFilesPath;
				}
				try
				{
					string text = ResolveDocumentsRoot();
					text = System.IO.Path.Combine(text, Utilities.GetApplicationName(), EngineFilePaths.ConfigsPath.Path, "ImprovedGarrisons", "Saves");
					if (text == null || text == "")
					{
						throw new Exception();
					}
					_saveFilesPath = text;
					return _saveFilesPath;
				}
				catch (Exception ex)
				{
					InformationManager.DisplayMessage(new InformationMessage("The file path for the Improved Garrisons config could not be initialized. The mods settings will not be saved!"));
					LogFileManager.WriteErrorLogEntry(MethodBase.GetCurrentMethod().Name, ex, withoutMessage: true);
					return null;
				}
			}
		}

		public bool PathIsValid()
		{
			return SaveFilesPath != null;
		}

		private static string ResolveDocumentsRoot()
		{
			if (Platform.GDKDesktop == ApplicationPlatform.CurrentPlatform)
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}

			string reflected = TryResolveReflectedDocumentsPath();
			if (!string.IsNullOrEmpty(reflected))
			{
				return reflected;
			}

			// The dedicated-server launcher installs its own IPlatformFileHelper wrapper that never
			// exposes DocumentsPath (only the interface members), so the reflection above finds nothing
			// there. Fall back to .NET's own equivalent -- it's what the real PlatformFileHelperPC.DocumentsPath
			// returns internally on this platform anyway.
			return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		}

		private static string TryResolveReflectedDocumentsPath()
		{
			try
			{
				PropertyInfo property = Common.PlatformFileHelper.GetType().GetProperty("DocumentsPath", BindingFlags.Instance | BindingFlags.NonPublic);
				return property != null ? (string)property.GetValue(Common.PlatformFileHelper) : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
