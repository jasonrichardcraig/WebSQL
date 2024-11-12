using Owin;

namespace WebSQL.Configuration
{
    public interface IStartup
    {
        void Configuration(IAppBuilder appBuilder);
    }
}
