using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System.IO;
using Xunit;

namespace configAndOptionsTest
{
    public class TestGetValue
    {
        public ConfigurationBuilder ConfigBuilder { get; }
        public IConfiguration Config { get; }

        public ConfigurationBuilder ConfigBuilderRoot { get; }
        public IConfiguration ConfigRoot { get; }


        public TestGetValue()
        {
            ConfigBuilder = new ConfigurationBuilder();
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObject.json"));
            Config = ConfigBuilder.Build();

            ConfigBuilderRoot = new ConfigurationBuilder();
            ConfigBuilderRoot.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObjectWithRoot.json"));
            ConfigRoot = ConfigBuilderRoot.Build();
        }


        /* "get value t" method will convert the configuratino value (string) to t,
           only basic types are supported, like int, stirng */

        // reference type cannot be converted from the string, 
        //   - get value <list> will return "null"
        //   - get value <dictionary> will return "null"
        //   - get value <complex> will return "null"
        //
        // default (null) will be returned if no related configuration exist

        [Fact]
        public void TestGetValueInt()
        {
            // get value from an exist configuration value (value object)
            var keyInt = Config.GetValue<int>("keyInt");           
            Assert.Equal(23, keyInt);

            // will get default t if no configuration value exist
            var key = Config.GetValue<int>("Key");
            Assert.Equal(0, key);
        }


        [Fact]
        public void TestGetValueString()
        {
            // get value from an exist configuration value (reference object)
            var keya = Config.GetValue<string>("Keya");
            Assert.Equal("valuea", keya);

            // get null (default) if no configuration value exist
            var key = Config.GetValue<string>("Key");
            Assert.Null(key);            
        }


        [Fact]
        public void TestGetValueList()
        {
            // CANNOT get value from a configuratino of list (list cannot convert from string)
            var subConfiglist = Config.GetValue<SubConfigList>("SubList");
            Assert.Null(subConfiglist);

            // will get null (default) if no configuration exist
            var list = Config.GetValue<SubConfigList>("key");
            Assert.Null(list);
        }


        [Fact]
        public void TestGetValueDictionary()
        {
            // CANNOT get value from an exist configuration (dictionary cannot convert from string)
            var subConfigDictionary = Config.GetValue<SubConfigDictionary>("SubDictionary");
            Assert.Null(subConfigDictionary);

            // will get null (default) if no configuration exist
            var dictionary = Config.GetValue<SubConfigDictionary>("key");
            Assert.Null(dictionary);           
        }


        [Fact]
        public void TestGetValueComplex()
        {
            // CANNOT get value from an exist configuration (complex cannot convert from string)
            var complex = ConfigRoot.GetValue<Config>("myConfig");
            Assert.Null(complex);

            // will get null (default) if no configuration exist
            var complex2 = Config.GetValue<Config>("key");
            Assert.Null(complex2);
        }
    }
}
