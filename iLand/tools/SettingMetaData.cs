using System;
using System.Collections.Generic;

namespace iLand.tools
{
    /** This helper class holds meta data (description, Urls, etc) about model settings.
       Various types of settings (species, model, ...) are stored together.
        */
    /** @class SettingMetaData
     This is some help text for the SettingMetaData class.
        */
    internal class SettingMetaData
    {
        private static readonly List<string> mTypeNames = new List<string>() { "invalid", "species", "model" };

        private Type mType;
        private string mName;
        private string mDescription;
        private string mUrl;
        private object mDefaultValue;

        public enum Type { SettingInvalid, SpeciesSetting, ModelSetting };

        // getters
        public object defaultValue() { return mDefaultValue; }
        public string url() { return mUrl; }
        public string name() { return mName; }
        public string description() { return mDescription; }

        public static Type typeFromName(string settingName)
        {
            Type retType = (Type)mTypeNames.IndexOf(settingName);
            if ((int)(retType) < 0)
            {
                retType = Type.SettingInvalid;
            }
            return retType;
        }

        public string typeName(Type type)
        {
            return mTypeNames[(int)type];
        }

        public SettingMetaData(Type type, string name, string description, string url, object defaultValue)
        {
            setValues(type, name, description, url, defaultValue);
        }

        public void setValues(Type type, string name, string description, string url, object defaultValue)
        {
            mType = type;
            mName = name;
            mDescription = description;
            mUrl = url;
            mDefaultValue = defaultValue;
        }

        public string dump()
        {
            string res = String.Format("Name: {0}{5}Type: {1} *** Default Value: {4} *** Url: {2} *** {5}Description: {3}",
                                       mName, mType, mUrl, mDescription, mDefaultValue, Environment.NewLine);
            return res;
        }
    }
}
