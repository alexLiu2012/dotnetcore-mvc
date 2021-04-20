using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace configAndOptionsTest
{
    public class TestOptions
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider Services { get; private set; }


        public TestOptions()
        {
            ServiceCollection = new ServiceCollection();
        }


        // "AddOptions()" method inject all options service into the di
        // "AddOptions<T>(name) method called "AddOptions()" inside, and the return new "options builder" instance
        // "options builder" will inject "configure options, post configure options, validate options" to the di

        [Fact]
        public void TestAddOptions()
        {
            /* add options will inject options service into di,
               thus get ioptions, ioptionsMonitor, ioptionsSnapshot will return a new instance (default by ctor) */
            ServiceCollection.Clear();
            ServiceCollection.AddOptions();
            Services = ServiceCollection.BuildServiceProvider();

            // get ioptions<t> (default) without "configure options, post configure options, validate options" registered
            var options = Services.GetService<IOptions<SubConfigList>>()?.Value;
            Assert.Equal(new SubConfigList(), options);

            // get ioptionsMonitor<t> (default) witout "configure options, post configure options, validate options" registered
            var optionsMonitor = Services.GetService<IOptionsMonitor<SubConfigList>>()?.CurrentValue;
            Assert.Equal(new SubConfigList(), optionsMonitor);

            // get ioptionsSnapshot<t> (default) without "configure options, post configure options, validate options" registered
            var optionsSnapshot = Services.GetService<IOptionsSnapshot<SubConfigList>>()?.Value;
            Assert.Equal(new SubConfigList(), optionsSnapshot);

        }

        
        // "IOptions<>" can ONLY support "unamed configure options", which will be configured or validated by options with name=string.empty (options.default name)
        // 
        // named configure options can be gotten by "IOptionsMonitor", with name as parameter,
        // "IOptionsMonitor.CurrentValue" will return the cached value with name=string.empty (options.default name) 
        // 
        // named configure options can be gotten by "IOptionsSnapshot", with name as parameter        
        // "IOptionsSnapShot.value" will return the cached value with name=string.empty (options.default name)

        [Fact]
        public void TestGetOptions()
        {
            /* test IOptions<T>, with "unamed configure options" return, which must be configured without name, or name=string.empty */

            var list = new SubConfigList()
            {
                "haha",
                "hehe"
            };

            ServiceCollection.Clear();

            // add options without name
            ServiceCollection.AddOptions<SubConfigList>().Configure(a => a.AddRange(list));
            Services = ServiceCollection.BuildServiceProvider();

            // iotions<T> will only return the unamed (name=string.empty) options
            var listOptions = Services.GetService<IOptions<SubConfigList>>();
            Assert.Equal(list, listOptions.Value);


            /* get named options by IOptionsSnapshot */
            var list1 = new SubConfigList()
            {
                "haha1",
                "hehe1"
            };

            ServiceCollection.Clear();

            // add options with name
            ServiceCollection.AddOptions<SubConfigList>("list1").Configure(a => a.AddRange(list1));
            Services = ServiceCollection.BuildServiceProvider();
            
            // get named options by IOptionsSnapshot
            var list1Options = Services.GetService<IOptionsSnapshot<SubConfigList>>().Get("list1");
            Assert.Equal(list1, list1Options);


            /* get named options by IOptionsMonitor */
            var list2 = new SubConfigList()
            {
                "haha2",
                "hehe2"
            };

            ServiceCollection.Clear();

            // add options with name
            ServiceCollection.AddOptions<SubConfigList>("list2").Configure(a => a.AddRange(list2));
            Services = ServiceCollection.BuildServiceProvider();

            // get named options by IOptionsMonitor
            var list2Options = Services.GetService<IOptionsMonitor<SubConfigList>>().Get("list2");
            Assert.Equal(list2, list2Options);            
        }

        
        // "services.configure()" method as same as "add options<t> & options builder.configure"
        [Fact]
        public void TestServiceConfigure()
        {
            /* configure <t> without name */
            ServiceCollection.Clear();

            var dictionary = new SubConfigDictionary()
            {
                {"SubKeya","SubValuea" },
                {"SubKeyb","SubValueb" },
                {"SubKeyc","SubValuec" }
            };

            ServiceCollection.Configure<SubConfigDictionary>(d =>
            {
                foreach (var item in dictionary)
                {
                    d.TryAdd(item.Key, item.Value);
                }
            });

            Services = ServiceCollection.BuildServiceProvider();

            var dictionaryOptions = Services.GetService<IOptions<SubConfigDictionary>>()?.Value;
            Assert.Equal(dictionary, dictionaryOptions);

            /* configure <t> with name */
            ServiceCollection.Clear();

            var dictionary2 = new SubConfigDictionary()
            {
                {"SubKeya","SubValuea2" },
                {"SubKeyb","SubValueb2" },
                {"SubKeyc","SubValuec2" }
            };

            ServiceCollection.Configure<SubConfigDictionary>("dict", d =>
            {
                foreach (var item in dictionary2)
                {
                    d.TryAdd(item.Key, item.Value);
                }
            });

            Services = ServiceCollection.BuildServiceProvider();

            var dicitonaryOptions2 = Services.GetService<IOptionsMonitor<SubConfigDictionary>>()?.Get("dict");
            Assert.Equal(dictionary2, dicitonaryOptions2);
        }


        // "service.configure(configuration)" method can bind the configuration to the options
        [Fact]
        public void TestServiceConfigureWithConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "SubList:0", "haha" },
                    { "SubList:1", "hehe" }
                })
                .Build();            

            ServiceCollection.Configure<SubConfigList>(configuration.GetSection("SubList"));

            Services = ServiceCollection.BuildServiceProvider();

            var options = Services.GetService<IOptions<SubConfigList>>()?.Value;
            Assert.Equal(
                new SubConfigList()
                {
                    "haha",
                    "hehe"
                },
                options);
        }

        

        /* "service.configure options()" method supply a very open way to defined all customized type to constraint the options.
           the customized type should implement "configure options", "post configure options" and/or "validate options",
           so such type will be injected into di with service "configure options", "post configure options" and/or "validate options" */

        // define customized type
        private class OptionsConfig : IConfigureOptions<Config>, IPostConfigureOptions<Config>, IValidateOptions<Config>
        {            
            public void Configure(Config options)
            {
                options.Keya = "value_configured";
            }

            public void PostConfigure(string name, Config options)
            {
                options.Keyb = "value_post_configured";
            }

            public ValidateOptionsResult Validate(string name, Config options)
            {
                return ValidateOptionsResult.Success;
            }
        }

        [Fact]
        public void TestServiceConfigureOptions()
        {
            // configure options with customized type
            ServiceCollection.Clear();           
            ServiceCollection.ConfigureOptions<OptionsConfig>();
            Services = ServiceCollection.BuildServiceProvider();

            var config = Services.GetService<IOptions<Config>>()?.Value;
            Assert.Equal("value_configured", config.Keya);
            Assert.Equal("value_post_configured", config.Keyb);


            // configure options with customized object
            ServiceCollection.Clear();
            var instance = new OptionsConfig();
            ServiceCollection.ConfigureOptions<OptionsConfig>();
            Services = ServiceCollection.BuildServiceProvider();

            var config2 = Services.GetService<IOptions<Config>>()?.Value;
            Assert.Equal("value_configured", config2.Keya);
            Assert.Equal("value_post_configured", config2.Keyb);


            // with name???
        }
    }
}
