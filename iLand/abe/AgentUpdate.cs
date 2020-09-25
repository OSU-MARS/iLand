using System;

namespace iLand.abe
{
    internal class AgentUpdate
    {
        private UpdateType mWhat;
        private string mValue; ///< new value of the given type
        private int mAge; ///< update should happen in that age
        private string mAfterActivity; ///< update should happen after given activity is executed

        private int mCounter; ///< number of stands that have not "seen" this update request

        public bool isValid() { return mCounter > 0; }
        public void setCounter(int n) { mCounter = n; }
        public void decrease() { --mCounter; }
        // getters
        public UpdateType type() { return mWhat; }
        public string value() { return mValue; }
        public string afterActivity() { return mAfterActivity; }
        public int age() { return mAge; }
        // setters
        public void setType(UpdateType type) { mWhat = type; }
        public void setValue(string new_value) { mValue = new_value; }
        public void setTimeAge(int age) { mAge = age; }
        // unused in c++
        // public void setTimeYear(int year) { mYear = year; }
        public void setTimeActivity(string act) { mAfterActivity = act; }

        public AgentUpdate()
        {
            mWhat = UpdateType.UpdateInvalid;
            mAge = -1;
            mCounter = 0;
        }

        public static UpdateType Label(string name)
        {
            if (name == "U")
            {
                return UpdateType.UpdateU;
            }
            if (name == "thinningIntensity")
            {
                return UpdateType.UpdateThinning;
            }
            if (name == "species")
            {
                return UpdateType.UpdateSpecies;
            }
            return UpdateType.UpdateInvalid;
        }

        public string Dump()
        {
            string line = (type()) switch
            {
                UpdateType.UpdateU => String.Format("AgentUpdate: update U to '{0}'.", mValue),
                UpdateType.UpdateThinning => String.Format("AgentUpdate: update thinning interval to '{0}'.", mValue),
                UpdateType.UpdateSpecies => String.Format("AgentUpdate: update species composition to '{0}'.", mValue),
                _ => throw new NotSupportedException() // includes UpdateType.UpdateInvalid
            };
            if (String.IsNullOrEmpty(mAfterActivity) == false)
            {
                return line + String.Format("Update after activity '{0}'.", mAfterActivity);
            }
            return line + String.Format("Update (before) age '{0}'.", mAge);
        }
    }
}
