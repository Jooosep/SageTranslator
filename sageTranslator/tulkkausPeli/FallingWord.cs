using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tulkkausPeli
{
    public class FallingWord
    {
        public string originalWord;
        public string translatedWord;
        public string hint;
        public string writeStr;
        public bool hasBeenTranslated;
        public int xPos;
        public decimal yPos;
        public decimal fallVelocity;
        public int YPos
        {
            get
            {
                return Decimal.ToInt32(Math.Round(yPos, 0, MidpointRounding.AwayFromZero));
            }

        }

        public FallingWord(string word, string translated, decimal speed, string hintStr)
        {
            originalWord = word;
            translatedWord = translated;
            fallVelocity = speed;
            hasBeenTranslated = false;
            hint = hintStr;
            writeStr = originalWord + hintStr;
        }
        public void Activate(int xPosition)
        {
            xPos = xPosition;
            yPos = 3;
        }
        public void Morph(string word, string translated)
        {
            originalWord = word;
            translatedWord = translated;
        }
    }
}
