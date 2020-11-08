﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static SDL2.SDL;
using static SDL2.SDL.SDL_EventType;
using Lua = KeraLua;
using Timer = System.Timers.Timer;

namespace Eight {
    public static class Eight {
        public const string Version = "Alpha 0.0.2";

        public static readonly string BaseDir = Directory.GetCurrentDirectory();
        public static readonly string LuaDir = Path.Combine(BaseDir, "lua");

        public const int DefaultWidth = 200;
        public const int DefaultHeight = 150;
        public const int DefaultScale = 2;
        public const int DefaultTickrate = 50;

        public static int WindowWidth;
        public static int WindowHeight;
        public static int WindowScale;

        public static int Tickrate;
        public static int Ticktime;

        public static int SyncTimeout = 3000;

        public static DateTime Epoch = DateTime.Now;

        private static bool _quit;
        private static SDL_Event e;

        public static void Main(string[] args) {
            Console.WriteLine($"Eight {Version}");
            Directory.SetCurrentDirectory(LuaDir);

            Init();
        }

        public static bool Init() {
            SetTickrate(DefaultTickrate);

            if (!Logic.Lua.Init()) {
                Console.WriteLine("Lua could not be initialized!");
                return false;
            }

            if (!Logic.SDL.Init()) {
                Console.WriteLine("SDL2 could not be initialized!");
                return false;
            }

            _quit = false;

            Parallel.Invoke(EventLoop, TickEmitter);

            return true;
        }

        public static void SetTickrate(int tickrate) {
            Tickrate = tickrate;
            Ticktime = 1000 / Tickrate;
        }

        public static void Resume(int n) {
            var syncTimer = new Timer {
                Enabled = true,
                AutoReset = false,
                Interval = SyncTimeout,
            };

            syncTimer.Elapsed += SyncTimerHandler;

            bool ok = Logic.Lua.Resume(n);
            
            syncTimer.Stop();
            if (!ok) {
                _quit = true;
            }
        }

        // TODO: kill lua if this ever happens, which is very likely, i caused this at least 10 times today.
        private static void SyncTimerHandler(object sender, ElapsedEventArgs ev) {
            Console.WriteLine("Warning: Lua State is out of sync!");
            Console.WriteLine("Caused after event: {0}", e.type);
        }

        private static void EventLoop() {
            int x, y;
            int oldX = -1;
            int oldY = -1;
            var pressedMouseButtons = new List<byte>();

            using var state = Logic.Lua.State;

            while (!_quit && SDL_WaitEvent(out e) != 0) {
                switch (e.type) {
                    case SDL_QUIT:
                        _quit = true;
                        break;
                    case SDL_KEYDOWN:
                    case SDL_KEYUP:

                        string keyName = SDL_GetKeyName(e.key.keysym.sym);
                        keyName = keyName.ToLower();
                        keyName = keyName.Replace(" ", "_");

                        state.PushString(e.key.state == SDL_PRESSED ? "key_down" : "key_up");
                        state.PushInteger((long) e.key.keysym.sym);
                        state.PushString(keyName);
                        state.PushBoolean(e.key.repeat != 0);

                        Resume(4);

                        break;
                    case SDL_TEXTINPUT:
                        byte[] c;
                        var a = e; // "You cannot use fixed size buffers contained in unfixed expressions. Try using the 'fixed' statement" my ass
                        unsafe {
                            c = Utils.CString(a.text.text); 
                        }

                        state.PushString("char");
                        state.PushBuffer(c);
                        Resume(2);

                        break;
                    case SDL_MOUSEMOTION:
                        x = e.motion.x / WindowScale;
                        y = e.motion.y / WindowScale;
                        if (oldX != x || oldY != y) {
                            state.PushString((pressedMouseButtons.Count > 0) ? "mouse_drag" : "mouse_hover");

                            if (pressedMouseButtons.Count > 0) {
                                state.PushInteger(pressedMouseButtons.Last());
                            }

                            state.PushInteger(x);
                            state.PushInteger(y);

                            Resume(pressedMouseButtons.Count > 0 ? 4 : 3);

                            oldX = x;
                            oldY = y;
                        }

                        break;
                    case SDL_MOUSEBUTTONDOWN:
                    case SDL_MOUSEBUTTONUP:
                        x = e.motion.x / WindowScale;
                        y = e.motion.y / WindowScale;

                        if (e.button.state == SDL_PRESSED) {
                            if (!pressedMouseButtons.Contains(e.button.button)) {
                                pressedMouseButtons.Add(e.button.button);
                            }
                        }
                        else {
                            if (pressedMouseButtons.Contains(e.button.button)) {
                                pressedMouseButtons.Remove(e.button.button);
                            }
                        }

                        state.PushString(e.button.state == SDL_PRESSED ? "mouse_click" : "mouse_up");
                        state.PushInteger(e.button.button);

                        state.PushInteger(x);
                        state.PushInteger(y);

                        state.PushBoolean(e.button.clicks != 1);

                        Resume(5);

                        break;
                    case SDL_MOUSEWHEEL:
                        x = e.wheel.x;
                        y = e.wheel.y;

                        if (SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED.Equals(e.wheel.direction)) {
                            x *= -1;
                            y *= -1;
                        }

                        if (y != 0 || x != 0) {
                            state.PushString("mouse_scroll");

                            state.PushInteger(x);
                            state.PushInteger(y);

                            state.PushInteger(oldX);
                            state.PushInteger(oldY);

                            Resume(5);
                        }

                        break;
                    case SDL_USEREVENT:
                        switch (e.user.code) {
                            case 0:
                                Logic.SDL.DrawCanvas();
                                state.PushString("_eight_tick");
                                Resume(1);
                                break;
                            case 1:
                                state.PushString("timer");
                                state.PushInteger((int) e.user.data1);
                                Resume(2);
                                break;
                        }

                        break;
                }
            }
            
            Console.WriteLine(SDL_GetError());
        }

        private static void TickEmitter() {
            while (!_quit) {
                var tickEvent = new SDL_Event {
                    type = SDL_USEREVENT,
                    user = {
                        code = 0
                    },
                };
                SDL_PushEvent(ref tickEvent);
                Thread.Sleep(Ticktime);
            }
        }

        public static void Quit() {
            Console.WriteLine("Quitting");
            _quit = true;
        }
    }
}