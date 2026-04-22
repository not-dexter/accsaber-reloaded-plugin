using System.Runtime.CompilerServices;
using System.Collections.Generic;
using IPA.Config.Stores.Converters;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig
	{
		public virtual bool RainbowHeader { get; set; } = false;

        [UseConverter(typeof(ListConverter<string>))]
        public List<string> Friends { get; set; } = new List<string>();
        public void AddFriend(string userid)
        {
            if (!IsFriend(userid))
            {
                Friends.Add(userid);
            }
        }

        public void RemoveFriend(string userid)
        {
            Friends.Remove(userid);
        }

        public bool IsFriend(string userid)
        {
            return Friends.Contains(userid);
        }
    }
}