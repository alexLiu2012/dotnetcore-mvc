using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using Xunit;

namespace configAndOptionsTest
{
    public class TestGet
    {
        public ConfigurationBuilder ConfigBuilder { get; }
        public IConfiguration Config { get; }

        public ConfigurationBuilder ConfigBuilderRoot { get; }
        public IConfiguration ConfigRoot { get; }


        public TestGet()
        {
            ConfigBuilder = new ConfigurationBuilder();
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObject.json"));
            Config = ConfigBuilder.Build();

            ConfigBuilderRoot = new ConfigurationBuilder();
            ConfigBuilderRoot.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObjectWithRoot.json"));
            ConfigRoot = ConfigBuilderRoot.Build();
        }


        /* get<T> method will either convert the value or bind the value to the specific configuration */

        // "get t" method called "return bindInstanc()" method to return the "NEW" object.
        // if CANNOT "get" the object, return the "default of t"
        //   -> return the default of t (value object)
        //   -> return null (reference object)
        // 
        // for "bindInstance()" method, it will return an object, from "convert value" or "binded value"
        //   - for convert value, convert string to t
        //   - for binded value, 
        //     + if configuration has no child, it is the leaf item but no value, return instance, the input target binded object!
        //     + if configuration has child (children), create a new object then bind value to (must has public ctor), otherwise throw exception!

        [Fact]
        public void TestGetInt()
        {
            // get an object binded from an exist configuration value (value object)
            var keyInt = Config.GetSection("keyInt").Get<int>();
            Assert.Equal(23, keyInt);

            // get default object if no configuration value exist (configuration without child)
            var key = Config.GetSection("Key").Get<int>();
            Assert.Equal(0, key);

            // get default object if no configuration value exist (configuratio with children)
            // can create a new object (default int)
            var key2 = Config.Get<int>();
            Assert.Equal(0, key2);            
        }


        [Fact]
        public void TestGetString()
        {                      
            // get an object binded from an exist configuration value (reference object)
            var keya = Config.GetSection("Keya").Get<string>();
            Assert.Equal("valuea", keya);

            // get null (default object) if no configuration value exist (configuration without child)
            var key = Config.GetSection("Key").Get<string>();
            Assert.Null(key);

            // get object if no configuration value exist (configuratio with children),
            // cannot create the instance of "string", -> throw exception
            var key2 = string.Empty;
            Assert.Throws<InvalidOperationException>(() => key2 = Config.Get<string>());            
        }


        [Fact]
        public void TestGetList()
        {
            // get an object binded from an exist configuration value
            var subConfiglist = Config.GetSection("SubList").Get<SubConfigList>();
            Assert.Equal(
                new SubConfigList()
                {
                    "haha",
                    "hehe"
                },
                subConfiglist);

            // get null (reference object) if no configuration value exist (configuration without section, return "instance=null")
            var list = Config.GetSection("key").Get<SubConfigList>();           
            Assert.Null(list);

            // get object if no configuration value exist (configuration with section),
            // can create the instance of "sub config list"!
            // but bind incorrect properties!!!
            var list2 = Config.Get<SubConfigList>();
        }


        [Fact]
        public void TestGetDictionary()
        {
            // get an object binded from an exist configuration value
            var subConfigDictionary = Config.GetSection("SubDictionary").Get<SubConfigDictionary>();
            Assert.Equal(
                new SubConfigDictionary()
                {
                    {"SubKeya","SubValuea" },
                    {"SubKeyb","SubValueb" },
                    {"SubKeyc","SubValuec" }
                },
                subConfigDictionary);

            // get object if no configuration value exist (configuration with section),
            // cannot create the instance of "sub config dictionary"
            //   - (key=string, cannot get generic type and create keys)
            //   - (will be ok for key=int/enum)
            var dictionary = new SubConfigDictionary();
            Assert.Throws<InvalidOperationException>(() => dictionary = Config.Get<SubConfigDictionary>());         
        }


        [Fact]
        public void TestGetComplex()
        {
            // get an object binded from an exist configuration value

            var config = new Config()
            {
                Keya = "valuea",
                Keyb = "valueb",
                Keyc = "valuec",
                SubList = new SubConfigList()
                {
                    "haha",
                    "hehe"
                },
                SubDictionary = new SubConfigDictionary()
                {
                    {"SubKeya","SubValuea" },
                    {"SubKeyb","SubValueb" },
                    {"SubKeyc","SubValuec" }
                }
            };
            
            var configGot = Config.Get<Config>();
            Assert.Equal(config, configGot);


            // get object if no configuration value exist (configuration with children),
            // can create instance of "config", -> get default "config"
            var config2 = new Config();
            var configGot2 = ConfigRoot.Get<Config>();
            Assert.True(true);
        }
                
    }
}
