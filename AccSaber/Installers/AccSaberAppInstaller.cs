using AccSaber.Utils;
using System;
using Zenject;

namespace AccSaber.Installers
{
#pragma warning disable IDE0290
    internal sealed class AccSaberAppInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind(typeof(IInitializable), typeof(IDisposable)).To<SerializerHandler>().AsSingle();
            //Container.Bind(typeof(IDisposable)).To<PlayerSocialLife>().AsSingle();
        }
    }
}
