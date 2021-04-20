using System.Collections.Generic;
using System;

namespace ConfigureationAndOptions
{
    public class SubConfigDictionary : Dictionary<string,string>
    {
        public override bool Equals(object obj)
        {
            if(obj is null)
            {
                return false;
            }

            if(!obj.GetType().IsAssignableFrom(typeof(SubConfigDictionary)))
            {
                return false;
            }

            var config = (SubConfigDictionary)obj;

            if(this.Count == config.Count)
            {
                foreach (var item in this)
                {
                    var key = item.Key;
                    var value = item.Value;

                    if (!config.TryGetValue(key, out var configValue) || value != configValue) 
                    {
                        return false;
                    }                    
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator==(SubConfigDictionary dictionary1, SubConfigDictionary dictionary2)
        {
            if(dictionary1 is null)
            {
                throw new ArgumentNullException(dictionary1.GetType().Name);
            }

            if(dictionary2 is null)
            {
                throw new ArgumentNullException(dictionary2.GetType().Name);
            }

            if (dictionary1.Equals(dictionary2))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool operator!=(SubConfigDictionary dictionary1, SubConfigDictionary dictionary2)
        {
            return !(dictionary1 == dictionary2);
        }
    }
}
