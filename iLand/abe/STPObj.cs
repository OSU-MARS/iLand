using iLand.tools;

namespace iLand.abe
{
    internal class STPObj
    {
        private FMSTP mSTP;
        private QJSValue mOptions; ///< options of the current STP

        public STPObj(object parent = null)
        {
            mSTP = null;
        }

        public QJSValue options() { return mOptions; }

        public void setSTP(FMStand stand)
        {
            if (stand != null && stand.stp() != null)
            {
                mSTP = stand.stp();
                mOptions = mSTP.JSoptions();
            }
            else
            {
                mOptions = null;
                mSTP = null;
            }
        }

        public string name()
        {
            if (mSTP != null)
            {
                return mSTP.name();
            }
            else
            {
                return "undefined";
            }
        }
    }
}
