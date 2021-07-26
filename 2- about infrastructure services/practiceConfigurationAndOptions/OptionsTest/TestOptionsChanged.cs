using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace OptionsTest
{
    public class TestOptionsChanged
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider Services { get; private set; }


        public TestOptionsChanged()
        {
            ServiceCollection = new ServiceCollection();
        }



        private void InvokeConfigProviderOnReload(IConfigurationRoot config)
        {            
            var provider = config.Providers.First();
            
            var tokenInvoker = typeof(ConfigurationProvider).GetMethod("OnReload", BindingFlags.NonPublic | BindingFlags.Instance);
            tokenInvoker.Invoke(provider, null);
        }


        // token registrations defined in "opitons monitor", with <options change token source, customized action of toptions & name>,
        // customized action was registered to a list of action, with topotins & name (named configure options)

        // ONLY services.configure(configuration) will inject implementation of "options change token source" in di service by default!
        // of cause can inject customized token, which should implement "options change token source" interface

        [Fact]
        public void TestChangedOptions()
        {
            var list = new SubConfigList()
            {
                "haha",
                "hehe"
            };

            var dict = new Dictionary<string, string>()
            {
                { "SubList:0", "haha" },
                { "Sublist:1", "hehe" }
            };

            // build config
            var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
                        
            // configure options
            ServiceCollection.Configure<SubConfigList>(config.GetSection("sublist"));
            Services = ServiceCollection.BuildServiceProvider();

            // get options monitor
            var listOptions = Services.GetService<IOptionsMonitor<SubConfigList>>();

            // register on change handler of action 
            var listAction = new SubConfigList();
            listOptions.OnChange(listener => listAction = listener);

            // register on change handler of action & string
            var listActionString = new SubConfigList();
            listOptions.OnChange((list, _) =>
            {
                listActionString = list;
            });

            // trigger changes of configuration
            config.Reload();        
            
            /* acutally, the changing of configuration comes from provider,
               to simulate it, we can trigger changes of configuration provider token 
                        
               // InvokeConfigProviderOnReload(config); */

            Assert.Equal(list, listAction);                        
            Assert.Equal(list, listActionString);                        
        }


        // named "options change token source" can be registered,
        // get changed options by the name

        [Fact]
        public void TestChangedOptionsWithName()
        {
            ServiceCollection.Clear();

            // list1
            var list1 = new SubConfigList()
            {
                "haha1",
                "hehe1"
            };

            var dict1 = new Dictionary<string, string>()
            {
                { "SubList:0", "haha1" },
                { "Sublist:1", "hehe1" }
            };

            // list2
            var list2 = new SubConfigList()
            {
                "haha2",
                "hehe2"
            };

            var dict2 = new Dictionary<string, string>()
            {
                { "SubList:0", "haha2" },
                { "Sublist:1", "hehe2" }
            };

            // build configs
            var config1 = new ConfigurationBuilder().AddInMemoryCollection(dict1).Build();
            var config2 = new ConfigurationBuilder().AddInMemoryCollection(dict2).Build();

            // configure options with "name"
            ServiceCollection
                .Configure<SubConfigList>("list1", config1.GetSection("sublist"))
                .Configure<SubConfigList>("list2", config2.GetSection("sublist"));

            Services = ServiceCollection.BuildServiceProvider();

            // get options monitor
            var optionsMonitor = Services.GetService<IOptionsMonitor<SubConfigList>>();
            
            // register on change handler
            var listChanged1 = new SubConfigList();
            var listChanged2 = new SubConfigList();
            
            // parameter "name" is used for switch the operations
            optionsMonitor.OnChange((list, name) =>
            {               
                if (name == "list1")
                {
                    listChanged1 = list;
                }

                if (name == "list2") 
                {
                    listChanged2 = list;
                }
            });


            // trigger config1 changing            
            config1.Reload();
            Assert.Equal(list1, listChanged1);

            // trigger config2 changing           
            config2.Reload();
            Assert.Equal(list2, listChanged2);
        }
    }
}
