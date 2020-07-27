using System;
using System.Collections.Generic;
using System.Linq;

using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;


namespace tulkkausPeli
{
    
    public static class InputExtensions
    {
        public static int LimitToRange(
            this int value, int inclusiveMinimum, int inclusiveMaximum)
        {
            if (value < inclusiveMinimum) { return inclusiveMinimum; }
            if (value > inclusiveMaximum) { return inclusiveMaximum; }
            return value;
        }

        private static Random shuffleRng = new Random();
 
        public static void Shuffle<T>(this IList<T> list)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // the code that you want to measure comes here
            
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = shuffleRng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            int debug  = 900;
        }
    }
    public class Dictionary
    {
        public string source;
        public string dest;
        public List<Tuple<string, string>> words;
        public List<string> sourceWords;
        public List<string> destWords;

        public Dictionary(string src, string dst)
        {
            source = src;
            dest = dst;
            words = new List<Tuple<string, string>>();
        }
        public override string ToString()
        {
            string description = "source language: " + source + "\n"
                + "destination language: " + dest + "\n"
                + "dictionary length: " + sourceWords.Count + " words\n";
            return description;
        }
    }
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

    class Program
    {
        static string[] countdown3 = 
            {"________ ",
            "\\_____  \\",
            "  _(__  <",
            " /       \\",
            "/______  /",
            "       \\/"

        };
        static string[] countdown2 =
            {"________ ",
            "\\_____  \\",
            " /  ____/",
            "/       \\ ",
            "\\_______ \\",
            "        \\/"

        };
        static string[] countdown1 =
            {" ____ ",
            "/_   |",
            " |   |",
            " |   |",
            " |   |",
            " |___|"

        };
        static string[] countdownGo =
            {"  ________ ________   ",
            " /  _____/ \\_____  \\  ",
            "/   \\  ___  /   |   \\ ",
            "\\    \\_\\  \\/    |    \\",
            " \\______  /\\_______  /",
            "        \\/         \\/ "

        };

        private static List<string[]> countdownList = new List<string[]>();
        private static List<Dictionary> allDictionaries = new List<Dictionary>();
        private static Dictionary selectedDictionary = null;
        private static bool reverseDictionary = false;
        private static readonly object balanceLock = new object();
        private static readonly System.ConsoleColor baseTextColor = ConsoleColor.White;
        private static readonly System.ConsoleColor translatedTextColor = ConsoleColor.Green;
        private static List<Tuple<string, string, string>> currentWordList = new List<Tuple<string, string, string>>();
        private static Queue<FallingWord> wordQueue = new Queue<FallingWord>();
        private static List<FallingWord> activeWords = new List<FallingWord>();
        private static List<Tuple<string, string, bool>> processedWords = new List<Tuple<string, string, bool>>();
        private static Random rand = new Random();
        private static int w = Console.WindowWidth;
        private static int h = Console.WindowHeight;
        private static bool launchNewWord = false;
        private static bool gamePaused = false;
        private static bool repeatWithSameWords;
        private static bool quitGame = false;
        private static bool endGameTrigger = false;
        private static int wordsToPlay = 100;
        private static readonly decimal baseSpeed = 0.2m;
        private static System.Timers.Timer gameUpdateTimer;
        private static System.Timers.Timer newWordTimer;

        private static int livesLeft;
        private static int score;

        //winapi functions to move console window
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);
        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;

        private enum HintType
        {
            None,
            NoHint,
            OneLetter,
            TwoLetters,
            Length
        }
        private static HintType currentHintType = HintType.None;

        private static void GameUpdate(Object source, System.Timers.ElapsedEventArgs e)
        {
            Console.CursorVisible = false;
            while (gamePaused)
            {
                Thread.Sleep(50);
            }
            w = Console.WindowWidth;
            h = Console.WindowHeight;
            lock (balanceLock)
            {
                if (launchNewWord && wordQueue.Count > 0)
                {
                    activeWords.Add(wordQueue.Dequeue());
                    activeWords.Last().Activate(rand.Next(w - activeWords.Last().writeStr.Length));
                    launchNewWord = false;
                }
                foreach (var word in activeWords)
                {
                    if (word.hasBeenTranslated)
                    {
                        WriteXY(word.xPos, word.YPos, word.originalWord, translatedTextColor);
                    }
                    else
                    {
                        WriteXY(word.xPos, word.YPos, word.writeStr, baseTextColor);
                    }
                    if (word.YPos > 0)
                    {
                        DeleteWord(word.xPos, word.YPos - 1, word.writeStr.Length);
                    }

                    word.yPos += word.fallVelocity;

                }
                List<int> removeIndexes = new List<int>();
                for (int i = activeWords.Count - 1; i >= 0; i--)
                {
                    if (activeWords[i].YPos >= h)
                    {
                        DeleteWord(activeWords[i].xPos, h - 1, activeWords[i].writeStr.Length);
                        removeIndexes.Add(i);
                    }
                }
                foreach (var index in removeIndexes)
                {
                    if(!activeWords[index].hasBeenTranslated)
                    {
                        livesLeft--;
                        DeleteWord(w - 20, 0, 40);
                        WriteUI();
                        if (livesLeft < 1)
                        {
                            endGameTrigger = true;
                            var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                            PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                        }
                    }

                    processedWords.Add(new Tuple<string, string, bool>(activeWords[index].originalWord, activeWords[index].translatedWord, activeWords[index].hasBeenTranslated));
                    
                    activeWords.RemoveAt(index);
                    if(processedWords.Count == wordsToPlay)
                    {
                        endGameTrigger = true;
                        var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                        PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                    }
                }
            }
        }

        private static void LaunchWord(Object source, System.Timers.ElapsedEventArgs e)
        {
            launchNewWord = true;
            
        }
        public static void WriteXY(int x, int y, string str, System.ConsoleColor color)
        {
            Console.ForegroundColor = color;
            x = x.LimitToRange(0, Console.WindowWidth - str.Length - 1);
            y = y.LimitToRange(0, Console.WindowHeight- 1);
            Console.SetCursorPosition(x, y);
            Console.Write(str);
            Console.ForegroundColor = baseTextColor;
        }

        public static void WriteArray(int x, int y, string[] array, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            int newY;
            int maxWidth = 0;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Length > maxWidth)
                {
                    maxWidth = array[i].Length;
                }
            }
            x -= maxWidth / 2;
            y -= array.Length / 2;
            for (int i = 0; i < array.Length; i++)
            {
                newY = y + i;
                x = x.LimitToRange(0, Console.WindowWidth - array[i].Length - 1);
                newY = newY.LimitToRange(0, Console.WindowHeight - 1);
                Console.SetCursorPosition(x, newY);
                Console.Write(array[i]);
            }
            
            Console.ForegroundColor = baseTextColor;
        }

        public static void DeleteWord(int x, int y, int len)
        {
            x = x.LimitToRange(0, Console.WindowWidth - len - 1);
            y = y.LimitToRange(0, Console.WindowHeight - 1);
            Console.SetCursorPosition(x + len, y);
            for (int i = 0; i < len; i++)
            {
                Console.Write("\b \b");
            }
 
        }
        public static void WriteUI()
        {
            WriteXY(w - 20, 0, "elämät: " + livesLeft, ConsoleColor.Red);
            WriteXY(w - 40, 0, "pisteet: " + score, ConsoleColor.Green);
        }
        public static void PrintMenu()
        {
            Console.WriteLine("Tervetuloa tulkkauspeliin! Valitse sanakirja syöttämällä sen numero tai sulje ohjelma painamalla ESC-näppäintä.\n");
            for (int i = 0; i < allDictionaries.Count; i++)
            {
                Console.WriteLine("sanakirja numero " + (i + 1) + ":\n");
                Console.WriteLine(allDictionaries[i].ToString());
            }
        }

        public static void PrintPauseMenu()
        {
            Console.WriteLine("Peli pysäytetty, syötä numero jatkaaksesi.\n");
            Console.WriteLine("1: Jatka peliä");
            Console.WriteLine("2: Lopeta peli");
        }
        public static void PrintScoreMessage()
        {
            Console.WriteLine("Your score is " + score + ". ");
            if (score == 0)
            {
                //Console.WriteLine("You are an embarrasment to society\n");
                Console.WriteLine("An attempt was made.\n");
            }
            else if(score < 10)
            {
                //Console.WriteLine("You = garbage.\n");
                Console.WriteLine("You have a long ways to go.\n");
            }
            else if (score < 20)
            {
                Console.WriteLine("You tried, but failed.\n");
            }
            else if(score < 30)
            {
                Console.WriteLine("You showed heart, but there is lots of room to improve.\n");
            }
            else if (score < 50)
            {
                Console.WriteLine("You are decently skilled. Keep on improving!\n");
            }
            else if (score < 80)
            {
                Console.WriteLine("Congratulations! You're pretty good.\n");
            }
            else if (score < 100)
            {
                Console.WriteLine("Congratulations! You're quite impressive.\n");
            }
            else if (score < 150)
            {
                Console.WriteLine("Congratulations! You are a master of translation.\n");
            }
            else if (score < 190)
            {
                Console.WriteLine("All hail the king of translation .\n");
            }
            else if (score < 200)
            {
                Console.WriteLine("Heavy is the head that encases a golden brain.\n");
            }
            else
            {
                Console.WriteLine("Hallowed be your name...\n");
            }
        }
        public static void EndScreen()
        {
            Console.Clear();
            PrintScoreMessage();
            Console.WriteLine("1: Return to start menu");
            Console.WriteLine("2: Play again with the same words\n");
            Console.WriteLine("Here is a list of all of the words that appeared in this game.\n");
            int cursorY = Console.CursorTop;
            cursorY += 2;
            int iter = 1;
            foreach (var word in processedWords)
            {
                
                if (word.Item3)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                Console.SetCursorPosition(5, cursorY);
                Console.WriteLine(iter + ". " + word.Item1 + " = " + word.Item2);
                Console.ForegroundColor = baseTextColor;
                iter++;
                cursorY++;
            }
            while (true)
            {
                var key = Console.ReadKey(true);
                int selNum;
                if (int.TryParse(key.KeyChar.ToString(), out selNum))
                {
                    if(selNum == 1)
                    {
                        repeatWithSameWords = false;
                        Console.Clear();
                        return;
                    }
                    else if (selNum == 2)
                    {
                        
                        Console.Clear();
                        repeatWithSameWords = true;
                        wordQueue.Clear();
                        activeWords.Clear();
                        processedWords.Clear();
                        currentWordList.Shuffle();
                        foreach (var word in currentWordList)
                        {
                            wordQueue.Enqueue(new FallingWord(word.Item1, word.Item2, baseSpeed, word.Item3));
                        }
                        return;
                    }
                }
            }
            
        }
        public static void MenuLoop()
        {
            wordQueue.Clear();
            activeWords.Clear();
            processedWords.Clear();
            currentWordList.Clear();
            selectedDictionary = null;
            currentHintType = HintType.None;
            reverseDictionary = false;
            PrintMenu();
            while (selectedDictionary == null)
            {

                var key = Console.ReadKey(true);
                int dictIndex;

                if (int.TryParse(key.KeyChar.ToString(), out dictIndex))
                {
                    if (dictIndex <= allDictionaries.Count && dictIndex > 0)
                    {
                        Console.Clear();
                        Console.WriteLine("Valitsit tämän sanakirjan: ");
                        Console.WriteLine(allDictionaries[dictIndex - 1].ToString());
                        while (currentHintType == 0)
                        {
                            Console.WriteLine("Valitse vielä tulkkaussuunta syöttämällä numero.");
                            Console.WriteLine("1: " + allDictionaries[dictIndex - 1].source + " -> " + allDictionaries[dictIndex - 1].dest);
                            Console.WriteLine("2: " + allDictionaries[dictIndex - 1].dest + " -> " + allDictionaries[dictIndex - 1].source);
                            Console.WriteLine("3: palaa takaisin");
                            var key2 = Console.ReadKey(true);
                            int dirIndex;
                            if (int.TryParse(key2.KeyChar.ToString(), out dirIndex))
                            {
                                if (dirIndex > 0 && dirIndex < 3)
                                {
                                    selectedDictionary = allDictionaries[dictIndex - 1];
                                    if (dirIndex == 2)
                                    {
                                        reverseDictionary = true;
                                    }
                                    Console.Clear();
                                    while (true)
                                    {
                                        Console.WriteLine("Valitse toivomasi vihje (vihje esiintyy sanan lopussa sulkujen sisällä.");
                                        Console.WriteLine("1: ei vihjettä");
                                        Console.WriteLine("2: ensimmäinen kirjain");
                                        Console.WriteLine("3: kaksi ensimmäistä kirjainta");
                                        Console.WriteLine("4: käännöksen merkkijonon pituus");
                                        var key3 = Console.ReadKey(true);
                                        int hintIndex;
                                        if (int.TryParse(key3.KeyChar.ToString(), out hintIndex))
                                        {
                                            if (hintIndex > 0 && hintIndex < 5)
                                            {
                                                currentHintType = (HintType)(hintIndex);
                                                break;
                                            }
                                        }
                                    }
                                    
                                }
                                else if (dirIndex == 3)
                                {
                                    Console.Clear();
                                    PrintMenu();
                                    break;
                                }
                            }

                        }
                    }
                }
                else if(key.Key == ConsoleKey.Escape)
                {
                    quitGame = true;
                    return;
                }
            }
                 
            int iter = 0;
            selectedDictionary.words.Shuffle();
            foreach (var word in selectedDictionary.words)
            {
                if (iter < wordsToPlay)
                {
                    if (reverseDictionary)
                    {
                        string hintStr = "";
                        switch(currentHintType)
                        {
                            case HintType.NoHint:
                                break;
                            case HintType.OneLetter:
                                hintStr = "(" + word.Item1.Substring(0, 1) + ")";
                                break;
                            case HintType.TwoLetters:
                                if(word.Item1.Length > 1)
                                    hintStr = "(" + word.Item1.Substring(0, 2) + ")";
                                break;
                            case HintType.Length:
                                hintStr = "(" + word.Item1.Length + ")";
                                break;
                        }
                        currentWordList.Add(new Tuple<string, string, string>(word.Item2, word.Item1, hintStr));
                        wordQueue.Enqueue(new FallingWord(word.Item2, word.Item1, baseSpeed, hintStr));
                    }
                    else
                    {
                        string hintStr = "";
                        switch (currentHintType)
                        {
                            case HintType.NoHint:
                                break;
                            case HintType.OneLetter:
                                hintStr = "(" + word.Item2.Substring(0, 1) + ")";
                                break;
                            case HintType.TwoLetters:
                                if (word.Item2.Length > 1)
                                    hintStr = "(" + word.Item2.Substring(0, 2) + ")";
                                break;
                            case HintType.Length:
                                hintStr = "(" + word.Item2.Length + ")";
                                break;
                        }
                        currentWordList.Add(new Tuple<string, string, string>(word.Item1, word.Item2, hintStr));
                        wordQueue.Enqueue(new FallingWord(word.Item1, word.Item2, baseSpeed, hintStr));
                    }
                }
                else
                {
                    break;
                }
                iter++;
            }
            return;
        }

        private static void GameLoop()
        {
            Thread.Sleep(1000);
            Console.Clear();
            launchNewWord = false;
            newWordTimer.Enabled = true;
            gameUpdateTimer.Enabled = true;         

            foreach (var strArray in countdownList)
            {
                WriteArray(w / 2, h / 2, strArray, ConsoleColor.Cyan);
                Thread.Sleep(1000);
                Console.Clear();
            }
            
            score = 0;
            livesLeft = 20;
            WriteUI();

            while(Console.KeyAvailable)
            {
                Console.ReadKey(true);
            }

            while (true)
            {
                string guess = "";
                while (true)
                {

                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        lock (balanceLock)
                        {
                            DeleteWord(0, 0, (guess.Length + 5));
                        }
                        if(endGameTrigger)
                        {
                            gameUpdateTimer.Enabled = false;
                            newWordTimer.Enabled = false;
                            Thread.Sleep(300);
                            return;
                        }
                        break;
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (guess.Length > 0)
                            guess = guess.Remove(guess.Length - 1);
                    }
                    else if (key.Key == ConsoleKey.Escape)
                    {
                        Pause();
                        while (true)
                        {
                            var selection = Console.ReadKey(true);
                            int selNum;
                            if (int.TryParse(selection.KeyChar.ToString(), out selNum))
                            {
                                if (selNum == 1)
                                {
                                    Unpause();
                                    break;
                                }
                                else if (selNum == 2)
                                {
                                    Console.Clear();
                                    gamePaused = false;
                                    gameUpdateTimer.Enabled = false;
                                    newWordTimer.Enabled = false;
                                    return;
                                }
                            }

                        }
                    }
                    else if (key.Key == ConsoleKey.U && key.Modifiers == ConsoleModifiers.Control)
                    {
                        guess += 'ü';
                        
                    }
                    else if (key.Key == ConsoleKey.O && key.Modifiers == ConsoleModifiers.Control)
                    {
                        guess += 'õ';
                    }
                    else
                    {
                        guess += key.KeyChar;
                    }
                    lock (balanceLock)
                    {
                        DeleteWord(0, 0, (guess.Length + 5));
                        WriteXY(0, 0, guess, baseTextColor);

                    }
                    while (Console.KeyAvailable)
                    {
                        Console.ReadKey(false);
                    }
                }
                foreach (var word in activeWords)
                {
                    if (word.translatedWord == guess)
                    {
                        score++;

                        lock (balanceLock)
                        {
                            WriteUI();
                        }
                        word.hasBeenTranslated = true;
                    }
                }

            }
        }

        private static void Pause()
        {
            gamePaused = true;
            gameUpdateTimer.Enabled = false;
            Thread.Sleep(100);
            Console.Clear();
            PrintPauseMenu();
        }
        private static void Unpause()
        {
            Console.Clear();
            gamePaused = false;
            WriteUI();
            gameUpdateTimer.Enabled = true;
            
        }

        static void Main(string[] args)
        {
            gameUpdateTimer = new System.Timers.Timer(100);
            gameUpdateTimer.AutoReset = true;
            gameUpdateTimer.Elapsed += GameUpdate;

            newWordTimer = new System.Timers.Timer(5000);
            newWordTimer.Elapsed += LaunchWord;
            newWordTimer.AutoReset = true;

            Console.OutputEncoding = System.Text.Encoding.Unicode;

            countdownList.Add(countdown3);
            countdownList.Add(countdown2);
            countdownList.Add(countdown1);
            countdownList.Add(countdownGo);

            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 0, 0, 100, 100, true);

            Console.SetWindowPosition(0, 0);

            string dir = System.IO.Directory.GetCurrentDirectory();
            //Console.WriteLine(dir);
            string[] files = System.IO.Directory.GetFiles(System.IO.Directory.GetCurrentDirectory());
            List<string> dictionaryFiles = new List<string>();
            foreach (var f in files)
            {
                string[] splitName = f.Split('\\');
                string fileName = splitName[splitName.Length - 1];
                if (fileName.Length > 4 && fileName.EndsWith(".txt") && fileName.Split('.')[0].Split('-').Length > 1 && fileName.Split('.')[0].Split('-')[0].Length > 1 && fileName.Split('.')[0].Split('-')[1].Length > 1)
                {
                    dictionaryFiles.Add(f);
                    string[] splitPath = f.Split('\\');
                }
            }
            //while (true) { };
            foreach (var dictFile in dictionaryFiles)
            {
                string[] splitName = dictFile.Split('\\');
                string fileName = splitName[splitName.Length - 1];
                string withoutExtension = fileName.Split('.')[0];
                string src = withoutExtension.Split('-')[0];
                string dst = withoutExtension.Split('-')[1];
                Dictionary newDict = new Dictionary(src, dst);
                newDict.sourceWords = new List<string>();
                newDict.destWords = new List<string>();

                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(@dictFile);
                while ((line = file.ReadLine()) != null)
                {
                    //System.Console.WriteLine(line);
                    string[] words = line.Split(',');
                    if (words.Length > 1)
                    {
                        newDict.sourceWords.Add(words[0]);
                        newDict.destWords.Add(words[1]);
                        newDict.words.Add(new Tuple<string, string>(words[0], words[1]));
                    }

                }
                allDictionaries.Add(newDict);

            }
            

            int windowW = Console.LargestWindowWidth;
            int windowH = Console.LargestWindowHeight;
            Console.SetWindowSize(windowW, windowH);
            w = Console.WindowWidth;
            h = Console.WindowHeight;
            
            Console.ForegroundColor = baseTextColor;
            Console.CursorVisible = false;

            if (allDictionaries.Count < 1)
            {
                Console.WriteLine("Sopivia sanakirjatiedostoja ei löytynyt. Etsitään .txt-loppuisia tiedostoja joiden nimi on väliviivalla erotettu lähde- ja kohdekielitunnus, esim. <en-fi.txt>.");
                Console.WriteLine("Paina Enter poistuaksesi ohjelmasta.");
                Console.ReadLine();
                return;
            }

            

            while (true)
            {

                if (!repeatWithSameWords)
                {
                    MenuLoop();
                    if (quitGame)
                        return;
                }
                
                GameLoop();
                if(endGameTrigger)
                {
                    endGameTrigger = false;
                    EndScreen();
                }
            }
        }
    }
}
