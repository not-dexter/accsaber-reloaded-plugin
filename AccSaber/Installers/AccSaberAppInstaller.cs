using AccSaber.API;
using AccSaber.Counter;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using Zenject;

namespace AccSaber.Installers
{
    internal sealed class AccSaberAppInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<SerializerUtils>().AsSingle();
            Container.BindInterfacesAndSelfTo<SerializationHandler>().AsSingle();
            Container.BindInterfacesAndSelfTo<AccsaberAPI>().AsSingle();
            Container.Bind<PlayerSocialLife>().AsSingle();
            Container.Bind<APCalc>().AsSingle();

            //Container.Bind(typeof(IDisposable)).To<PlayerSocialLife>().AsSingle();
        }
    }
}
