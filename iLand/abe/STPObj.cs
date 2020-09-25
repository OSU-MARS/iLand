using iLand.tools;

namespace iLand.abe
{
    internal class STPObj
    {
        private FMSTP mSTP;
        private QJSValue mOptions; ///< options of the current STP
        public QJSValue options() { return mOptions; }

        public STPObj()
        {
            mSTP = null;
        }

        public void SetStp(FMStand stand)
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

        public string Name()
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
