using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace iLand.Tools
{
    /** This helper class holds meta data (description, Urls, etc) about model settings.
       Various types of settings (species, model, ...) are stored together.
        */
    /** @class SettingMetaData
     This is some help text for the SettingMetaData class.
        */
    public class SettingMetaData
    {
        private static readonly ReadOnlyCollection<string> TypeNames = new List<string>() { "invalid", "species", "model" }.AsReadOnly();

        public enum Type { SettingInvalid, SpeciesSetting, ModelSetting };
        private Type mType;

        public object DefaultValue { get; private set; }
        public string Description { get; private set; }
        public string Name { get; private set; }
        public string Url { get; private set; }

        public static Type TypeFromName(string settingName)
        {
            Type retType = (Type)TypeNames.IndexOf(settingName);
            if ((int)(retType) < 0)
            {
                retType = Type.SettingInvalid;
            }
            return retType;
        }

        public string TypeName(Type type)
        {
            return TypeNames[(int)type];
        }

        public SettingMetaData(Type type, string name, string description, string url, object defaultValue)
        {
            SetValues(type, name, description, url, defaultValue);
        }

        public void SetValues(Type type, string name, string description, string url, object defaultValue)
        {
            mType = type;
            Name = name;
            Description = description;
            Url = url;
            DefaultValue = defaultValue;
        }

        public string Dump()
        {
            string res = String.Format("Name: {0}{5}Type: {1} *** Default Value: {4} *** Url: {2} *** {5}Description: {3}",
                                       Name, mType, Url, Description, DefaultValue, Environment.NewLine);
            return res;
        }
    }
}
