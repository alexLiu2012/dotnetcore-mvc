namespace ConfigureationAndOptions
{
    public class Config
    {
        public string Keya { get; set; }
        public string Keyb { get; set; }
        public string Keyc { get; set; }

        public SubConfigList SubList { get; set; }
        public SubConfigDictionary SubDictionary { get; set; }

        public override bool Equals(object obj)
        {          
            if(obj is null)
            {
                return false;
            }

            if(!obj.GetType().IsAssignableFrom(typeof(Config)))
            {
                return false;
            }

            var config = (Config)obj;

            if (this.Keya != config.Keya ||
                this.Keyb != config.Keyb ||
                this.Keyc != config.Keyc ||
                this.SubList != config.SubList ||
                this.SubDictionary != config.SubDictionary)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
