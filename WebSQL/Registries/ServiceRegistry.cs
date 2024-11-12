using Microsoft.Owin.Host.HttpListener;
using Microsoft.Owin.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting.Channels;
using WebSQL.Configuration;

namespace WebSQL.Registries
{
    public class ServiceRegistry
    {
        private static readonly ConcurrentDictionary<string, IDisposable> _webApps = new ConcurrentDictionary<string, IDisposable>();

        public static bool StartWebApp(string baseAddress)
        {
            if (baseAddress == null)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }
            else if (_webApps.ContainsKey(baseAddress))
            {
                throw new InvalidOperationException($"WebApp already started at {baseAddress}");
            }

            var options = new StartOptions(baseAddress);
            options.ServerFactory = typeof(OwinServerFactory).AssemblyQualifiedName;
            var webApp = WebApp.Start<Startup>(options);

            return _webApps.TryAdd(baseAddress, webApp);

        }

        public static bool StopWebApp(string baseAddress)
        {
            if (baseAddress == null)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }
            else if (!_webApps.ContainsKey(baseAddress))
            {
                throw new InvalidOperationException($"WebApp does not exist at {baseAddress}");
            }

            if (_webApps.TryRemove(baseAddress, out IDisposable webApp))
            {
                webApp.Dispose();
                return true;
            }
            return false;
        }

        public static IEnumerable<string> GetWebApps()
        {
            return _webApps.Keys;
        }

    }
}
