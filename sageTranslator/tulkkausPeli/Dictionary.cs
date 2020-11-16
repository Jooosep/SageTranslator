using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tulkkausPeli
{
    public class Dictionary
    {
        public string source;
        public string dest;
        public string description;
        public List<Tuple<string, string>> words;

        public Dictionary(string src, string dst, string desc)
        {
            source = src;
            dest = dst;
            description = desc;
            words = new List<Tuple<string, string>>();
        }
        public override string ToString()
        {
            string margin = "     ";
            string str = description + "\n" + margin
                + "source language: " + source + "\n" + margin
                + "destination language: " + dest + "\n" + margin
                + "dictionary length: " + words.Count + " words\n";
            return str;
        }
    }
}
