using System;

namespace XNAPacMan {
    static class Program {
        static void Main(string[] args) {
            using (XNAPacMan game = new XNAPacMan()) {
                game.Run();
            }
        }
    }
}

