using AccSaber.Configuration;
using AccSaber.Installers;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using System.Net.Http;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AccSaber
{
	[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
	public class Plugin
	{
		internal static DiContainer Container = null!;
		internal static HttpClient WebClient = null!;
		[Init]
		public void Init(Zenjector zenjector, IPALogger logger, Config config)
		{
			zenjector.UseLogger(logger);
			zenjector.UseHttpService();
			WebClient = new();
			WebClient.BaseAddress = new System.Uri("https://api.accsaberreloaded.com/");
            
			zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
		}
	}
}