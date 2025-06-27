using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WonderGame.Screens;

namespace WonderGame.Core
{
    public class CommandProcessor
    {
        private readonly Action<string> _queueOutput;
        private readonly Func<IsometricScreen> _createIsometricScreen;
        
        // Interaction tracking for "look harder" unlock
        private readonly HashSet<string> _interactedObjects = new();
        private bool _lookHarderUnlocked = false;
        private bool _hasSeenPostItNote = false;

        public CommandProcessor(Action<string> queueOutput, Func<IsometricScreen> createIsometricScreen)
        {
            _queueOutput = queueOutput;
            _createIsometricScreen = createIsometricScreen;
        }

        public IScreen? ProcessCommand(string input)
        {
            var trimmedInput = input.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInput)) return null;

            var parts = trimmedInput.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = NormalizeCommand(parts[0]);
            var args = FilterCommonWords(parts.Skip(1).ToArray());

            switch (verb)
            {
                case "help":
                    HandleHelp();
                    break;
                    
                case "look":
                    return HandleLook(args);
                    
                case "examine":
                    HandleExamine(args);
                    break;
                    
                case "touch":
                    HandleTouch(args);
                    break;

                case "inventory":
                    HandleInventory();
                    break;
                    
                case "exit":
                case "quit":
                    HandleExit();
                    break;
                    
                case "clear":
                    HandleClear();
                    break;
                    
                case "say":
                    HandleSay(args);
                    break;
                    
                default:
                    _queueOutput($"> Unknown command: '{parts[0]}'{(args.Length > 0 ? $" {string.Join(" ", args)}" : "")}");
                    break;
            }
            
            return null;
        }

        // Normalize command synonyms to canonical commands
        private string NormalizeCommand(string command)
        {
            var commandSynonyms = new Dictionary<string, string>
            {
                // Look synonyms
                ["observe"] = "look",
                ["see"] = "look",
                ["view"] = "look",
                ["watch"] = "look",
                ["peek"] = "look",
                ["glance"] = "look",
                
                // Examine synonyms
                ["inspect"] = "examine",
                ["check"] = "examine",
                ["study"] = "examine",
                ["investigate"] = "examine",
                ["analyze"] = "examine",
                ["read"] = "examine",
                
                // Touch synonyms
                ["feel"] = "touch",
                ["grab"] = "touch",
                ["hold"] = "touch",
                ["handle"] = "touch",
                ["press"] = "touch",
                
                // Exit synonyms
                ["leave"] = "exit",
                ["escape"] = "exit",
                ["q"] = "quit",
                
                // Clear synonyms
                ["cls"] = "clear",
                ["clr"] = "clear",
                
                // Help synonyms
                ["?"] = "help",
                ["commands"] = "help",
                
                // Say synonyms
                ["speak"] = "say",
                ["talk"] = "say",
                ["tell"] = "say",
                ["shout"] = "say",
                ["whisper"] = "say"
            };

            return commandSynonyms.TryGetValue(command, out var canonical) ? canonical : command;
        }

        // Filter out common prepositions, articles, and filler words that don't affect meaning
        private string[] FilterCommonWords(string[] words)
        {
            var fillerWords = new HashSet<string> 
            { 
                "at", "the", "a", "an", "on", "in", "with", "to", "into", "onto", "upon", "from", "of", "over", "under", "around"
            };
            
            return words.Where(word => !fillerWords.Contains(word)).ToArray();
        }

        // Find recognized objects in the command, regardless of position
        private string FindRecognizedObject(string[] words)
        {
            // Define all known objects and their synonyms
            var knownObjects = new Dictionary<string, List<string>>
            {
                ["sign"] = new() { "sign", "poster", "plaque" },
                ["terminal"] = new() { "terminal", "computer", "screen", "monitor", "console" },
                ["bunk"] = new() { "bunk", "bed", "cot" },
                ["walls"] = new() { "wall", "walls" },
                ["post-it"] = new() { "post-it", "postit", "note", "post-it note", "postit note", "sticky note", "yellow note" },
                ["room"] = new() { "room", "area", "chamber", "space" }
            };

            // Try to find multi-word objects first (like "post-it note")
            var fullText = string.Join(" ", words);
            foreach (var kvp in knownObjects)
            {
                foreach (var synonym in kvp.Value.OrderByDescending(s => s.Length)) // Check longer phrases first
                {
                    if (fullText.Contains(synonym))
                    {
                        return kvp.Key; // Return the canonical name
                    }
                }
            }

            // If no multi-word match, try individual words
            foreach (var word in words)
            {
                foreach (var kvp in knownObjects)
                {
                    if (kvp.Value.Contains(word))
                    {
                        return kvp.Key;
                    }
                }
            }

            // If nothing found, return the filtered args as before
            return words.Length > 0 ? string.Join(" ", words) : "";
        }

        private void HandleHelp()
        {
            _queueOutput("> Available commands:");
            _queueOutput(">   help  - Shows this help message.");
            _queueOutput(">   clear - Clears the screen.");
            _queueOutput(">   exit  - Exits the game.");
            _queueOutput(">   look  - Examines your surroundings.");
            _queueOutput(">   examine [object] - Examines a specific object.");
            _queueOutput(">   touch [object] - Touches a specific object.");
            _queueOutput(">   say [phrase] - Speak aloud.");
            _queueOutput(">   inventory - Shows your inventory.");
        }

        private IScreen? HandleLook(string[] args)
        {
            if (args.Length == 0)
            {
                // Look around the room
                _queueOutput("> You are in a small square room. The walls are featureless metal, tinted green by");
                _queueOutput("> flickering fluorescent strips above. There is a terminal in front of you, a bunk");
                _queueOutput("> bolted to the wall, and an old sign, partially scratched off.");
            }
            else if (args.Length == 1 && args[0] == "harder")
            {
                // "look harder" command
                if (!_lookHarderUnlocked)
                {
                    _queueOutput("> You try to focus, but the command feels unfamiliar. Maybe you need more context first?");
                }
                else
                {
                    _queueOutput("> You focus harder. The humming intensifies. The walls begin to shimmer...");
                    _queueOutput("> [TRANSITION TO DEEP SYSTEM MODE INITIATED]");
                    return _createIsometricScreen();
                }
            }
            else
            {
                // Look at specific object - use smart object recognition
                var target = FindRecognizedObject(args);
                ExamineObject(target);
            }
            
            return null;
        }

        private void HandleExamine(string[] args)
        {
            if (args.Length == 0)
            {
                _queueOutput("> What would you like to examine?");
                return;
            }

            var target = FindRecognizedObject(args);
            ExamineObject(target);
        }

        private void HandleTouch(string[] args)
        {
            if (args.Length == 0)
            {
                _queueOutput("> What would you like to touch?");
                return;
            }

            var target = FindRecognizedObject(args);
            TouchObject(target);
        }

        private void ExamineObject(string target)
        {
            switch (target)
            {
                case "sign":
                    _interactedObjects.Add("sign");
                    CheckInteractionUnlock();
                    _queueOutput("> The sign reads: \"____ YOUR POSTS. THE DRAGON IS ALWAYS LISTENING.\"");
                    break;
                    
                case "terminal":
                    _interactedObjects.Add("terminal");
                    _queueOutput("> It displays a looping warning:");
                    _queueOutput("> \"EMOTIONAL SUPPRESSION FIELD ACTIVE. HOSTILE ENTITY DETECTED IN SECTOR C.\"");
                    _queueOutput("> There's a small yellow post-it note stuck to the bottom corner of the screen.");
                    break;
                    
                case "bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    _queueOutput("> Cold. Uninviting. There's a sticker on the underside: \"You are not the first, nor");
                    _queueOutput("> the last. Tell the Dragon it smells.\"");
                    break;
                    
                case "walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    _queueOutput("> Smooth metal panels, slightly warm to the touch. The faint vibration suggests machinery");
                    _queueOutput("> behind them. Something feels... off. Wrong. Like reality is thinner here.");
                    break;
                    
                case "post-it":
                    _interactedObjects.Add("post-it");
                    _hasSeenPostItNote = true;
                    CheckInteractionUnlock();
                    _queueOutput("> A faded yellow sticky note. In shaky handwriting: \"The walls are not what they seem.");
                    _queueOutput("> Look harder when you've seen everything. Trust me. - J\"");
                    break;
                    
                case "room":
                    _queueOutput("> You are in a small square room. The walls are featureless metal, tinted green by");
                    _queueOutput("> flickering fluorescent strips above. There is a terminal in front of you, a bunk");
                    _queueOutput("> bolted to the wall, and an old sign, partially scratched off.");
                    break;
                    
                default:
                    _queueOutput($"> You can't examine '{target}' from here.");
                    break;
            }
        }

        private void TouchObject(string target)
        {
            switch (target)
            {
                case "sign":
                    _interactedObjects.Add("sign");
                    CheckInteractionUnlock();
                    _queueOutput("> The metal is cold and slightly corroded. You can feel scratches where");
                    _queueOutput("> letters have been carved out.");
                    break;
                    
                case "terminal":
                    _interactedObjects.Add("terminal");
                    _queueOutput("> The screen is warm. A faint electrical hum. The post-it note crinkles");
                    _queueOutput("> under your finger.");
                    break;
                    
                case "bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    _queueOutput("> Hard. Uncomfortable. There's a strange warmth, like someone was just here.");
                    break;
                    
                case "walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    _queueOutput("> Smooth metal panels. They vibrate faintly when you touch them. Something moves");
                    _queueOutput("> behind the walls - or maybe that's just your imagination.");
                    break;
                    
                case "post-it":
                    _interactedObjects.Add("post-it");
                    _hasSeenPostItNote = true;
                    CheckInteractionUnlock();
                    _queueOutput("> The paper is old and brittle. The ink has faded, but you can still make out");
                    _queueOutput("> the urgent scrawl: \"Look harder.\"");
                    break;
                    
                default:
                    _queueOutput($"> You can't touch '{target}' from here.");
                    break;
            }
        }

        private void CheckInteractionUnlock()
        {
            // Unlock "look harder" when player has seen the post-it note AND interacted with at least 2 room objects
            if (_hasSeenPostItNote && _interactedObjects.Count >= 2 && !_lookHarderUnlocked)
            {
                _lookHarderUnlocked = true;
                _queueOutput("> [Something clicks in your mind. The post-it note's message suddenly feels significant.]");
                _queueOutput("> [You feel like you could 'look harder' now.]");
            }
        }

        private void HandleInventory()
        {
            _queueOutput("> You have nothing in your pockets except for a sense of unease.");
        }

        private void HandleExit()
        {
            // Note: Actual exit logic should be handled by MainScreen
            _queueOutput("> Use ESC to exit the game.");
        }

        private void HandleClear()
        {
            // Note: Actual clear logic should be handled by MainScreen
            _queueOutput("> [SCREEN CLEARED]");
        }

        private void HandleSay(string[] args)
        {
            if (args.Length == 0)
            {
                _queueOutput("> What would you like to say?");
                return;
            }

            var phrase = string.Join(" ", args);
            _queueOutput($"> You say: \"{phrase}\"");
            
            if (phrase.ToLowerInvariant().Contains("dragon") && phrase.ToLowerInvariant().Contains("smell"))
            {
                _queueOutput("> [A distant roar echoes through the walls. Something stirs...]");
            }
            else
            {
                _queueOutput("> Your words echo briefly in the small room, then fade.");
            }
        }
    }
} 