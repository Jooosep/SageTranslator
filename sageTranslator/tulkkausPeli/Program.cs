using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Globalization;

namespace tulkkausPeli
{

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
        private static readonly string[] instructionLines =
        {
            "When the game starts, words in the chosen source language will start falling from the top of the window.",
            "Translate the word to the chosen destination language before it reaches the bottom end of the window.",
            "Simply write the translation and press Enter. If the word you attempted to translate was translated correctly it will turn green and one point will be added to the gamescore.",
            "The word you are writing will appear at the top left of the window, score and remaining lives will be shown at the top right side.",
            "The game will end when all lives are lost by letting untranslated words reach the bottom of the window or when there are no more words left to translate.",
            "There are no penalties for attempted translations that fail so you can use the Enter key to quickly clear what you are writing instead of backspace.",
            "The game can be paused by pressing the ESC key.",
            "There are two special characters that require ctrl to be pressed. Firstly ü, which appears with ctrl+u and secondly õ, which appears with ctrl+o.",
            "In the options you can modify some key parameters of the game: amount of lives, word falling speed, word interval and total words."
        };

        private static readonly string[] settingsFileInstructionalText = {"#Here you can set wordInterval in ms, for example <wordInterval=10000>, default is 5000, min 1000",
                                                            "#fallSpeed in a decimal number with one decimal, e.g. <fallSpeed=0.1>, default is 0.2, max 1.0",
                                                            "#lives at the start of a game, e.g. <lives=20>, default is 10",
                                                            "#maxWords during single game, e.g. <maxWords=50>, default is 100"};
        private enum HintType
        {
            None,
            NoHint,
            OneLetter,
            TwoLetters,
            Length
        }

        private static HintType currentHintType = HintType.None;

        private static List<string[]> countdownList = new List<string[]>();
        
        private static readonly object writeToConsoleLock = new object();

        public static readonly System.ConsoleColor baseTextColor = ConsoleColor.White;
        public static readonly System.ConsoleColor translatedTextColor = ConsoleColor.Green;
        public static int xMargin = 2;

        private static List<Tuple<string, string, string>> currentWordList = new List<Tuple<string, string, string>>();
        private static Queue<FallingWord> wordQueue = new Queue<FallingWord>();
        private static List<FallingWord> activeWords = new List<FallingWord>();
        private static List<Tuple<string, string, bool>> processedWords = new List<Tuple<string, string, bool>>();
        private static List<Dictionary> allDictionaries = new List<Dictionary>();
        private static Dictionary selectedDictionary = null;

        private static readonly Random rand = new Random();
        private static int w = Console.WindowWidth;
        private static int h = Console.WindowHeight;

        private static bool launchNewWord = false;
        private static bool gamePaused = false;
        private static bool repeatWithSameWords = false;
        private static bool quitGame = false;
        private static bool endGameTrigger = false;
        private static bool reverseDictionary = false;

        private static int wordsToPlay = 100;
        private static decimal baseSpeed = 0.2m;
        private static int startingLives = 10;
        private static int wordInterval = 5000;

        private static System.Timers.Timer gameUpdateTimer;
        private static System.Timers.Timer newWordTimer;

        private static OptionsMenu dictionarySelector;
        private static int livesLeft;
        private static int score;

        //winapi functions to move console window and other windows functions
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleDisplayMode(out int lpModeFlags);


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);
        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(
            IntPtr hConsoleHandle,
            out int lpMode);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(
            IntPtr hConsoleHandle,
            int ioMode);

        const int QuickEditMode = 64;

        const int ExtendedFlags = 128;

        const int STD_INPUT_HANDLE = -10;


        private static void DisableQuickEdit()
        {
            IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
            if (!GetConsoleMode(consoleHandle, out int mode))
            {
                Console.WriteLine(Marshal.GetLastWin32Error());
                throw new Exception();
            }
            
            mode = mode & ~(QuickEditMode | ExtendedFlags);

            if (!SetConsoleMode(consoleHandle, mode))
            {
                Console.WriteLine("error");
                // error setting console mode.
            }
        }

        private static int PollNumber(int min, int max)
        {
            
            while (true)
            {
                var key = Console.ReadKey(true);
                if(int.TryParse(key.KeyChar.ToString(), out int num))
                {
                    if (num >= min && num <= max)
                    {
                        return num;
                    }
                }
            }
        }

        private static void GameUpdate(Object source, System.Timers.ElapsedEventArgs e)
        {
            Console.CursorVisible = false;
            while (gamePaused)
            {
                Thread.Sleep(50);
            }
            w = Console.WindowWidth;
            h = Console.WindowHeight;
            lock (writeToConsoleLock)
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
                            foreach (var word in activeWords)
                            {
                                if (word.hasBeenTranslated)
                                {
                                    processedWords.Add(new Tuple<string, string, bool>(word.originalWord, word.translatedWord, word.hasBeenTranslated));
                                }

                            }
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
            if (!gamePaused)
            {
                launchNewWord = true;
            }
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
            WriteXY(w - 20, 0, "lives: " + livesLeft, ConsoleColor.Red);
            WriteXY(w - 40, 0, "score: " + score, ConsoleColor.Green);
        }


        public static void PrintPauseMenu()
        {
            WriteXY(xMargin, 1, "Game paused.", baseTextColor);
            WriteXY(xMargin, 3, "1: Resume", baseTextColor);
            WriteXY(xMargin, 4, "2: Quit game", baseTextColor);
        }
        public static string GetScoreMessage()
        {
            return "Your score is " + score + "/" + processedWords.Count + ".";
        }
        public static void EndScreen()
        {
            Console.Clear();
            WriteXY(xMargin, 1, GetScoreMessage(), baseTextColor);
            WriteXY(xMargin, 3, "1: Return to start menu", baseTextColor);
            WriteXY(xMargin, 4, "2: Play again with the same words", baseTextColor);
            WriteXY(xMargin, 6, "Here is a list of all of the words that appeared in this game.", baseTextColor);
            int cursorY = 8;
            int iter = 1;
            foreach (var word in processedWords)
            {
                ConsoleColor color = baseTextColor;
                if (word.Item3)
                {
                    color = ConsoleColor.Green;
                }
                Console.SetCursorPosition(xMargin, cursorY);
                WriteXY(xMargin, cursorY, iter + ". " + word.Item1 + " = " + word.Item2, color);
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
            while (true)
            {
                Console.CursorVisible = false;
                Console.Clear();
                string[] mainMenuLines =
                {
                    "Welcome to the speed translation game!",
                    "",
                    "1: Play",
                    "2: Instructions",
                    "3: Options",
                    "4: Quit"
                };
                for (int i = 0; i < mainMenuLines.Length; i++)
                {
                    WriteXY(xMargin, i + 1, mainMenuLines[i], baseTextColor);
                }

                int startMenuSelection = PollNumber(1, 4);
                Console.CursorVisible = false;
                if (startMenuSelection == 1)
                {
                    while (true)
                    {
                        int dictIndex = dictionarySelector.Poll();
                        if (dictIndex == -1)
                        {
                            break;
                        }

                        wordQueue.Clear();
                        activeWords.Clear();
                        processedWords.Clear();
                        currentWordList.Clear();
                        selectedDictionary = null;
                        currentHintType = HintType.None;
                        reverseDictionary = false;
                        while (true)
                        {
                            Console.Clear();
                            string[] chooseDirLines =
                            {
                                "Selected dictionary: " + allDictionaries[dictIndex].description,
                                "",
                                "Choose which language is source and which is destination",
                                "1: " + allDictionaries[dictIndex].source + " -> " + allDictionaries[dictIndex].dest,
                                "2: " + allDictionaries[dictIndex].dest + " -> " + allDictionaries[dictIndex].source,
                                "3: return"
                            };
                            for (int i = 0; i < chooseDirLines.Length; i++)
                            {
                                WriteXY(xMargin, i + 1, chooseDirLines[i], baseTextColor);
                            }

                            int selectedOption = PollNumber(1, 3);
                            if (selectedOption == 1)
                            {
                                reverseDictionary = false;
                            }
                            else if (selectedOption == 2)
                            {
                                reverseDictionary = true;
                            }
                            else
                            {
                                break;
                            }

                            Console.Clear();
                            string[] chooseHintLines =
                            {
                                "Choose what type of hint you want (the hint will appear in brackets after the word)",
                                "1: no hint",
                                "2: hint is first letter",
                                "3: hint is two first letters",
                                "4: hint is string length",
                                "5: return"
                            };
                            for (int i = 0; i < chooseHintLines.Length; i++)
                            {
                                WriteXY(xMargin, i + 1, chooseHintLines[i], baseTextColor);
                            }

                            int hintIndex = PollNumber(1, 5);
                            if (hintIndex != 5)
                            {
                                currentHintType = (HintType)(hintIndex);
                                selectedDictionary = allDictionaries[dictIndex];
                                if (selectedDictionary.words.Count < wordsToPlay)
                                {
                                    wordsToPlay = selectedDictionary.words.Count;
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
                                            switch (currentHintType)
                                            {
                                                case HintType.NoHint:
                                                    break;
                                                case HintType.OneLetter:
                                                    hintStr = "(" + word.Item1.Substring(0, 1) + ")";
                                                    break;
                                                case HintType.TwoLetters:
                                                    if (word.Item1.Length > 1)
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
                        }
                    }
                    
                    
                }
                else if (startMenuSelection == 2)
                {
                    Console.Clear();
                    WriteXY(xMargin, 1, "Press any key to return...", baseTextColor);
                    WriteXY(xMargin, 3, "Instructions:", baseTextColor);

                    int yPos = 5;
                    int iter = 1;
                    foreach (var line in instructionLines)
                    {
                        WriteXY(xMargin, yPos,iter + ": " + line, baseTextColor);
                        yPos++;
                        iter++;
                    }
                    Console.ReadKey(true);
                }
                else if (startMenuSelection == 3)
                {
                    int userResponseLine = 2;
                    int errorLine = 4;
                    while (true)
                    {
                        Console.Clear();
                        string[] chooseOption =
                        {
                            "Options:",
                            "",
                            "1: Set lives at the start of the game",
                            "2: Set word falling speed",
                            "3: Set interval between words",
                            "4: Set total words during one game",
                            "5: Return"
                        };
                        for (int i = 0; i < chooseOption.Length; i++)
                        {
                            WriteXY(xMargin, i + 1, chooseOption[i], baseTextColor);
                        }

                        int optionsSelection = PollNumber(1, 5);
                        if (optionsSelection == 1)
                        {
                            Console.Clear();
                            WriteXY(xMargin, 1 , "Set amount of lives (min=1), currently " + startingLives + ", or return with Enter", baseTextColor);
                            while (true)
                            {
                                Console.SetCursorPosition(xMargin, 2);
                                string input = Console.ReadLine();
                                if (int.TryParse(input, out int newAmountOfLives))
                                {
                                    if (newAmountOfLives > 0)
                                    {
                                        startingLives = newAmountOfLives;
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be over 0", baseTextColor);
                                }
                                else
                                {
                                    if (input == "")
                                    {
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be an integer.", baseTextColor);
                                }
                            }
                        }
                        else if (optionsSelection == 2)
                        {
                            Console.Clear();
                            WriteXY(xMargin, 1, "Set word falling speed (0.1 - 1.0 with one decimal), currently " + baseSpeed + ", or return with Enter", baseTextColor);

                            NumberStyles style = NumberStyles.AllowDecimalPoint;
                            while (true)
                            {
                                Console.SetCursorPosition(xMargin, userResponseLine);
                                string input = Console.ReadLine();
                                if (Decimal.TryParse(input, style, CultureInfo.InvariantCulture, out decimal newFallSpeed))
                                {
                                    if (newFallSpeed.ToString().Length > 3)
                                    {
                                        DeleteWord(0, errorLine, 30);
                                        DeleteWord(0, userResponseLine, 30);
                                        WriteXY(xMargin, errorLine, "Only one decimal allowed", baseTextColor);
                                    }
                                    else if (newFallSpeed < 0.1m || newFallSpeed > 1.0m)
                                    {
                                        DeleteWord(0, errorLine, 30);
                                        DeleteWord(0, userResponseLine, 30);
                                        WriteXY(xMargin, errorLine, "Minimum 0.1, maximum 1.0", baseTextColor); 
                                    }
                                    else
                                    {
                                        baseSpeed = newFallSpeed;
                                        break;
                                    }

                                }
                                else
                                {
                                    if (input == "")
                                    {
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be a decimal.", baseTextColor);
                                }
                            }
                        }
                        else if (optionsSelection == 3)
                        {
                            Console.Clear();
                            WriteXY(xMargin, 1, "Set interval between words in ms (min=1000), currently " + wordInterval + ", or return with Enter", baseTextColor);

                            while (true)
                            {
                                Console.SetCursorPosition(xMargin, userResponseLine);
                                string input = Console.ReadLine();
                                if (int.TryParse(input, out int newInterval))
                                {
                                    if (newInterval > 999)
                                    {
                                        wordInterval = newInterval;
                                        newWordTimer = new System.Timers.Timer(wordInterval);
                                        newWordTimer.Elapsed += LaunchWord;
                                        newWordTimer.AutoReset = true;
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be at least 1000", baseTextColor);
                                }
                                else
                                {
                                    if(input == "")
                                    {
                                        break;
                                    }
                                    DeleteWord(0, userResponseLine, 30);
                                    DeleteWord(0, errorLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be an integer.", baseTextColor);
                                }
                                
                            }
                        }
                        else if (optionsSelection == 4)
                        {
                            Console.Clear();
                            WriteXY(xMargin, 1, "Set amount of total words to appear (min=1), currently " + wordsToPlay + ", or return with Enter", baseTextColor);
                            while (true)
                            {
                                Console.SetCursorPosition(xMargin, userResponseLine);
                                string input = Console.ReadLine();
                                if (int.TryParse(input, out int newWordTotal))
                                {
                                    if (newWordTotal > 0)
                                    {
                                        wordsToPlay = newWordTotal;
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be over 0", baseTextColor);
                                }
                                else
                                {
                                    if (input == "")
                                    {
                                        break;
                                    }
                                    DeleteWord(0, errorLine, 30);
                                    DeleteWord(0, userResponseLine, 30);
                                    WriteXY(xMargin, errorLine, "Must be an integer.", baseTextColor);
                                }
                            }
                        }
                        else if (optionsSelection == 5)
                        {
                            break;
                        }

                        string dir = System.IO.Directory.GetCurrentDirectory();
                        string settingsPath = dir + "\\settings.txt";
                        using (System.IO.StreamWriter file =
                        new System.IO.StreamWriter(@settingsPath))
                        {
                            foreach (string line in settingsFileInstructionalText)
                            {
                                file.WriteLine(line);
                            }
                            file.WriteLine("wordInterval=" + wordInterval);
                            file.WriteLine("fallSpeed=" + baseSpeed);
                            file.WriteLine("lives=" + startingLives);
                            file.WriteLine("maxWords=" + wordsToPlay);

                        }

                    }
                }
                else
                {
                    quitGame = true;
                    return;
                }
            }
        }

        private static void GameLoop()
        {

            Thread.Sleep(300);
            Console.Clear();
            foreach (var strArray in countdownList)
            {
                WriteArray(w / 2, h / 2, strArray, ConsoleColor.Green);
                Thread.Sleep(1000);
                Console.Clear();
            }
            
            score = 0;
            livesLeft = startingLives;
            WriteUI();

            while(Console.KeyAvailable)
            {
                Console.ReadKey(true);
            }

            launchNewWord = true;
            newWordTimer.Enabled = true;
            gameUpdateTimer.Enabled = true;

            while (true)
            {
                string guess = "";
                while (true)
                {

                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        lock (writeToConsoleLock)
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
                    lock (writeToConsoleLock)
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

                        lock (writeToConsoleLock)
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

            Console.OutputEncoding = System.Text.Encoding.Unicode;

            countdownList.Add(countdown3);
            countdownList.Add(countdown2);
            countdownList.Add(countdown1);
            countdownList.Add(countdownGo);

            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 0, 0, 100, 100, true);
            
            Console.SetBufferSize(100, Console.WindowHeight + 1);
            Console.SetWindowPosition(0, 0);
            DisableQuickEdit();

            string dir = System.IO.Directory.GetCurrentDirectory();
            //Console.WriteLine(dir);
            string settingsFilePath = "";
            string[] files = System.IO.Directory.GetFiles(System.IO.Directory.GetCurrentDirectory());
            List<string> dictionaryFiles = new List<string>();
            foreach (var f in files)
            {
                string[] splitName = f.Split('\\');
                string fileName = splitName[splitName.Length - 1];
                if (fileName == "settings.txt")
                {

                    settingsFilePath = f;
                }
                if (fileName.Length > 4 && fileName.EndsWith(".txt") && fileName.Split('.')[0].Split('-').Length > 1 && fileName.Split('.')[0].Split('-')[0].Length > 1 && fileName.Split('.')[0].Split('-')[1].Length > 1)
                {
                    dictionaryFiles.Add(f);
                    string[] splitPath = f.Split('\\');
                }
            }

            if (settingsFilePath.Length > 0)
            {
                NumberStyles style = NumberStyles.AllowDecimalPoint;
                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(@settingsFilePath);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Length > 0 && line[0] != '#')
                    {

                        if (line.Split('=').Length > 0)
                        {
                            
                            if (line.Split('=')[0] == "fallSpeed")
                            {
                                string decim = line.Split('=')[1];
                                if (Decimal.TryParse(line.Split('=')[1], style, CultureInfo.InvariantCulture, out decimal res))
                                {
                                    if (res > 0.0m && res <= 1.0m)
                                    {
                                        baseSpeed = res;
                                    }
                                }
                            }
                            if (line.Split('=')[0] == "wordInterval")
                            {
                                if (int.TryParse(line.Split('=')[1], out int res))
                                {
                                    if (res >= 1000 )
                                    {
                                        wordInterval = res;
                                    }
                                }
                            }
                            if (line.Split('=')[0] == "lives")
                            {
                                if (int.TryParse(line.Split('=')[1], out int res))
                                {
                                    if (res > 0)
                                    {
                                        startingLives = res;
                                    }
                                }
                            }
                            if (line.Split('=')[0] == "maxWords")
                            {
                                if (int.TryParse(line.Split('=')[1], out int res))
                                {
                                    if (res > 0)
                                    {
                                        wordsToPlay = res;
                                    }
                                }
                            }
                        }
                    }
                }
                file.Close();
            }

            foreach (var dictFile in dictionaryFiles)
            {
                string[] splitName = dictFile.Split('\\');
                
                string fileName = splitName[splitName.Length - 1];
                
                string withoutExtension = fileName.Split('.')[0];
                string src = withoutExtension.Split('-')[0];
                string dst = withoutExtension.Split('-')[1];
                Dictionary newDict = new Dictionary(src, dst, "");
                bool descriptionFound = false;

                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(@dictFile);
                while ((line = file.ReadLine()) != null)
                {

                    if (line.Length > 0 && line[0] != '#')
                    {

                        string[] words = line.Split(',');
                        if (words.Length > 1 && words[0].Length > 0 && words[1].Length > 0)
                        {
                            newDict.words.Add(new Tuple<string, string>(words[0], words[1]));
                        }
                    }
                    else
                    {
                        if(line.Length > 1)
                        {
                            if(!descriptionFound)
                            {
                                descriptionFound = true;
                            }
                            newDict.description += line.Split('#')[1];
                        }
                    }

                }
                if (newDict.words.Count > 1)
                {
                    allDictionaries.Add(newDict);
                }
                if(!descriptionFound)
                {
                    newDict.description = "no description found";
                }
            }
            string dictMenuTitle = "First choose a dictionary or return with ESC\n";
            string[] dictionaryOptions = new string[allDictionaries.Count];
            for (int i = 0; i < allDictionaries.Count; i++)
            {
                dictionaryOptions[i] = allDictionaries[i].ToString();
            }
            dictionarySelector = new OptionsMenu(dictMenuTitle, dictionaryOptions);

            int windowW = Console.LargestWindowWidth;
            int windowH = Console.LargestWindowHeight;
            Console.SetWindowSize(windowW, windowH);
            w = Console.WindowWidth;
            h = Console.WindowHeight;
            
            Console.ForegroundColor = baseTextColor;
            Console.CursorVisible = false;

            if (allDictionaries.Count < 1)
            {
                Console.WriteLine("No suitable dictionary files found. Looking for txt-files the name of which is the source and destination language codes separated by a hyphen, e.g. <en-fi.txt>.");
                Console.WriteLine("Press Enter to exit the program.");
                Console.ReadLine();
                return;
            }

            gameUpdateTimer = new System.Timers.Timer(100);
            gameUpdateTimer.AutoReset = true;
            gameUpdateTimer.Elapsed += GameUpdate;

            newWordTimer = new System.Timers.Timer(wordInterval);
            newWordTimer.Elapsed += LaunchWord;
            newWordTimer.AutoReset = true;

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
