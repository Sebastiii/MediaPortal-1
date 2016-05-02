using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaPortal.Player;
using MediaPortal.GUI.Library;
using MediaPortal.Plugins.BDHandler.Player;
using MediaPortal.Profile;

namespace MediaPortal.Plugins.BDHandler
{

  public class FactoryWrapper : IPlayerFactory
  {
    IPlayerFactory defaultPlayerFactory;

    public FactoryWrapper(IPlayerFactory factory)
    {
      this.defaultPlayerFactory = factory;
    }

    public IPlayerFactory GetDefaultFactory()
    {
      return this.defaultPlayerFactory;
    }

    public IPlayer Create(string filename)
    {
      return Create(filename, g_Player.MediaType.Video);
    }

    public IPlayer Create(string filename, g_Player.MediaType type)
    {
      using (Settings xmlreader = new MPSettings())
      {
        bool BDInternalMenu = xmlreader.GetValueAsBool("bdplayer", "useInternalBDPlayer", true);

        if (!BDInternalMenu)
        {
          string filepath = filename.ToLowerInvariant();

          if (filepath.Length < 4)
          {
            string discPath = Path.Combine(filepath, @"BDMV\index.bdmv");
            if (File.Exists(discPath))
            {
              return GetBlurayPlayer();
            }
          }

          if (filepath.EndsWith(".mpls") || filepath.EndsWith(".bdmv") || (filepath.Contains(@"bdmv\stream") && filepath.EndsWith(".m2ts")))
          {
            return GetBlurayPlayer();
          }

          return this.defaultPlayerFactory.Create(filename, type);
        }
        else
        {
          return this.defaultPlayerFactory.Create(filename, type);
        }
      }
    }

    public static BDPlayer GetBlurayPlayer()
    {
      BDPlayer player = new BDPlayer();
      player.SourceFilter = BDHandlerCore.Filter;
      return player;
    }
  }
}