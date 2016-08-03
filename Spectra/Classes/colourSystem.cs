using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spectra
{
    public class colourSystem
    {
        public string name;
        public double xRed, yRed,              /* Red x, y */
        xGreen, yGreen,         /* Green x, y */
        xBlue, yBlue,           /* Blue x, y */
        xWhite, yWhite,         /* White point x, y */
        gamma;                  /* Gamma correction for system */
        public colourSystem(string n, double xr, double yr, double xg, double yg, double xb, double yb, double xw, double yw, double g)
        {
            this.name = n;
            this.xRed = xr;
            this.yRed = yr;
            this.xGreen = xg;
            this.yGreen = yg;
            this.xBlue = xb;
            this.yBlue = yb;
            this.xWhite = xw;
            this.yWhite = yw;
            this.gamma = g;

        }
    }
}
