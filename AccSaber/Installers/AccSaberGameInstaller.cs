using AccSaber.ScoreTracking;
using Zenject;

namespace AccSaber.Installers
{
    internal class AccSaberGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<ScoreCounter>().AsSingle();
        }
    }
}
