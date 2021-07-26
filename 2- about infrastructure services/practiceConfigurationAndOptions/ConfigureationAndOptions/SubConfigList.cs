using System.Collections.Generic;
using System;

namespace ConfigureationAndOptions
{
    public class SubConfigList : List<string>
    {
        public SubConfigList()
        {

        }

        public override bool Equals(object obj)
        {
            if(obj is null)
            {
                return false;
            }

            if(!obj.GetType().IsAssignableFrom(typeof(SubConfigList)))
            {
                return false;
            }

            var config = (SubConfigList)obj;

            if(this.Count == config.Count)
            {
                for (int i = 0; i < this.Count; i++)
                {
                    if(this[i] != config[i])
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

        public static bool operator==(SubConfigList list1, SubConfigList list2)
        {
            if(list1 is null)
            {
                throw new ArgumentNullException(list1.GetType().Name);
            }

            if(list2 is null)
            {
                throw new ArgumentNullException(list2.GetType().Name);

            }

            if(list1.Equals(list2))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool operator!=(SubConfigList list1, SubConfigList list2)
        {
            return !(list1 == list2);
        }
    }
}
