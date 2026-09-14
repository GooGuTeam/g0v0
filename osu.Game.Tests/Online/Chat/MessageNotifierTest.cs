// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Online.Chat;

namespace osu.Game.Tests.Online.Chat
{
    [TestFixture]
    public class MessageNotifierTest
    {
        [Test]
        public void TestContainsUsernameMidlinePositive()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("This is a test message", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameStartOfLinePositive()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("Test message", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameEndOfLinePositive()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("This is a test", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameMidlineNegative()
        {
            ClassicAssert.False(MessageNotifier.MatchUsername("This is a testmessage for notifications", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameStartOfLineNegative()
        {
            ClassicAssert.False(MessageNotifier.MatchUsername("Testmessage", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameEndOfLineNegative()
        {
            ClassicAssert.False(MessageNotifier.MatchUsername("This is a notificationtest", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameBetweenPunctuation()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("Hello 'test'-message", "Test").Success);
        }

        [Test]
        public void TestContainsUsernameUnicode()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("Test \u0460\u0460 message", "\u0460\u0460").Success);
        }

        [Test]
        public void TestContainsUsernameUnicodeNegative()
        {
            ClassicAssert.False(MessageNotifier.MatchUsername("Test ha\u0460\u0460o message", "\u0460\u0460").Success);
        }

        [Test]
        public void TestContainsUsernameSpecialCharactersPositive()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("Test [#^-^#] message", "[#^-^#]").Success);
        }

        [Test]
        public void TestContainsUsernameSpecialCharactersNegative()
        {
            ClassicAssert.False(MessageNotifier.MatchUsername("Test pad[#^-^#]oru message", "[#^-^#]").Success);
        }

        [Test]
        public void TestContainsUsernameAtSign()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("@username hi", "username").Success);
        }

        [Test]
        public void TestContainsUsernameColon()
        {
            ClassicAssert.True(MessageNotifier.MatchUsername("username: hi", "username").Success);
        }

        [Test]
        public void TestContainsUsernameCjkWithDelimiter()
        {
            // CJK usernames are matched when delimited by punctuation or other non-word characters.
            ClassicAssert.True(MessageNotifier.MatchUsername("张雪峰：你跑不过我你信吗", "张雪峰").Success);
            ClassicAssert.True(MessageNotifier.MatchUsername("@张雪峰 你跑不过我你信吗", "张雪峰").Success);
        }

        [Test]
        public void TestContainsUsernameCjkAdjacentNegative()
        {
            // CJK letters are Unicode word characters (the server-side username validation in
            // g0v0-server#189 permits Unicode usernames), so a username directly adjacent to other
            // CJK text is part of a longer word and must not be matched as a standalone mention.
            ClassicAssert.False(MessageNotifier.MatchUsername("张雪峰你跑不过我你信吗", "张雪峰").Success);
            ClassicAssert.False(MessageNotifier.MatchUsername("好张雪峰强", "张雪峰").Success);
        }

        [Test]
        public void TestMatchUsernameFuzz()
        {
            // Randomised consistency check against a naive reference implementation. Generated usernames
            // only use characters accepted by the server-side username validation (see g0v0-server#189).
            var rng = new Random(1337);

            for (int i = 0; i < 2000; i++)
            {
                string username = createRandomUsername(rng);
                string message = createRandomMessage(rng, username);

                bool actual = MessageNotifier.MatchUsername(message, username).Success;
                bool expected = matchesWholeWord(message, username);

                ClassicAssert.AreEqual(expected, actual, $@"username ""{username}"" in message ""{message}""");
            }
        }

        private static string createRandomUsername(Random rng)
        {
            int length = rng.Next(2, 16);

            var builder = new StringBuilder(length);
            bool containsNonSpace = false;

            for (int i = 0; i < length; i++)
            {
                char c = username_chars[rng.Next(username_chars.Length)];
                containsNonSpace |= c != ' ';
                builder.Append(c);
            }

            // An all-space username would make its underscore variant empty and degenerate the pattern.
            if (!containsNonSpace)
                builder[0] = 'a';

            return builder.ToString();
        }

        private static string createRandomMessage(Random rng, string username)
        {
            // Around half the messages embed the username (or its underscore variant) in a random context.
            var builder = new StringBuilder();

            if (rng.Next(2) == 0)
            {
                string name = rng.Next(2) == 0 ? username : username.Replace(' ', '_');

                bool atStart = rng.Next(2) == 0;
                bool atEnd = rng.Next(2) == 0;

                if (!atStart)
                    builder.Append(message_chars[rng.Next(message_chars.Length)]);

                builder.Append(name);

                if (!atEnd)
                    builder.Append(message_chars[rng.Next(message_chars.Length)]);
            }

            for (int i = 0, count = rng.Next(0, 10); i < count; i++)
                builder.Append(message_chars[rng.Next(message_chars.Length)]);

            return builder.ToString();
        }

        /// <summary>
        /// Naive reference implementation of <see cref="MessageNotifier.MatchUsername"/>: whether the username
        /// (or its underscore variant) occurs in the message without being adjacent to a Unicode word character.
        /// </summary>
        private static bool matchesWholeWord(string message, string username)
            => containsWholeWord(message, username) || containsWholeWord(message, username.Replace(' ', '_'));

        private static bool containsWholeWord(string message, string word)
        {
            int index = -1;

            while ((index = message.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                bool precededByWordChar = index > 0 && isWordChar(message[index - 1]);
                bool followedByWordChar = index + word.Length < message.Length && isWordChar(message[index + word.Length]);

                if (!precededByWordChar && !followedByWordChar)
                    return true;
            }

            return false;
        }

        private static bool isWordChar(char c)
            => char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;

        private const string username_chars = "abcXYZ019 _-[]张雪峰";
        private const string message_chars = "abcXYZ019 _-[]张雪峰：@!.'?";
    }
}
