using iLand.Tool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace iLand.Test
{
    [TestClass]
    public class ExpressionTest
    {
        [TestMethod]
        public void SpecializedForms()
        {
            // tree species' upper and lower height:diameter ratio bounds
            ExpressionHeightDiameterRatioBounded heightDiameterBound1 = new();
            heightDiameterBound1.Parse("min(0.85 * 170.7057 * 1.6 * d ^ (-0.28932 * 1.9), 110)");
            ExpressionHeightDiameterRatioBounded heightDiameterBound2 = new();
            heightDiameterBound2.Parse("min(63.3574*1.2*d^(-0.08445*2),110)");
            ExpressionHeightDiameterRatioBounded heightDiameterBound3 = new();
            heightDiameterBound3.Parse("min(94.45233*d^-0.21264,110)");
            ExpressionHeightDiameterRatioBounded heightDiameterBound4 = new();
            heightDiameterBound4.Parse("0.8*220.545*1.002*(1-0.2834)*d^-0.2834");

            Assert.IsTrue(heightDiameterBound1.Evaluate(1.0F) == 110.0F);
            Assert.IsTrue(heightDiameterBound1.Evaluate(10.0F) == 65.4755249F);
            Assert.IsTrue(heightDiameterBound2.Evaluate(2.0F) == 67.6292648F);
            Assert.IsTrue(heightDiameterBound2.Evaluate(20.0F) == 45.83895F);
            Assert.IsTrue(heightDiameterBound3.Evaluate(3.0F) == 74.7752F);
            Assert.IsTrue(heightDiameterBound3.Evaluate(30.0F) == 45.8266F);
            Assert.IsTrue(heightDiameterBound4.Evaluate(4.0F) == 85.52793F);
            Assert.IsTrue(heightDiameterBound4.Evaluate(40.0F) == 44.5356636F);

            // aging
            ExpressionAging aging1 = new();
            aging1.Parse("1/(1 + (x/0.95)^4)");
            ExpressionAging aging2 = new();
            aging2.Parse("1/(1 + (x/0.8)^2.05)");

            Assert.IsTrue(aging1.Evaluate(0.6F) == 0.8627273F);
            Assert.IsTrue(aging2.Evaluate(0.7F) == 0.5680107F);

            // sapling height growth potential
            //"1.2*72.2*(1-(1-(h/72.2)^(1/3))*exp(-0.0427))^3";
            //"49.4*(1-(1-(h/49.4)^(1/3))*exp(-0.0476))^3";
            //"28.985*(1-(1-(h/28.985)^(1/3))*exp(-0.0609))^3"
        }
    }
}
