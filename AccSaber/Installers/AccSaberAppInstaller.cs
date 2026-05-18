using AccSaber.Utils;
using System;
using Zenject;

namespace AccSaber.Installers
{
    internal class AccSaberAppInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind(typeof(IInitializable), typeof(IDisposable)).To<SerializerHandler>().AsSingle();
        }
    }
}
