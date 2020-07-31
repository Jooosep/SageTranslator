using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tulkkausPeli
{
    public class OptionsMenu
    {
        public string titleString;
        public string[] optionStrings;

        public OptionsMenu(string titleStr, string[] optionStrs)
        {
            titleString = titleStr;
            optionStrings = optionStrs;
        }

        //return index of menu or -1 on ESC
        public int Poll()
        {
            Console.Clear();
            Console.WriteLine(titleString);
            for (int i = 0; i < optionStrings.Length; i++)
            {
                Console.WriteLine((i + 1) + ": " + optionStrings[i]);
            }
            while (true)
            {
                var key = Console.ReadKey(true);
                if (int.TryParse(key.KeyChar.ToString(), out int selectedOption))
                {
                    if (selectedOption > 0 && selectedOption <= optionStrings.Length)
                    {
                        Console.Clear();
                        return selectedOption - 1;
                    }
                }
                else if(key.Key == ConsoleKey.Escape)
                {
                    return -1;
                }
            }

        }

    }
}
