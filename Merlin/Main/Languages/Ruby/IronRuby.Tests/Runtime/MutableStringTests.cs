﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using IronRuby.Builtins;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using IronRuby.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronRuby.Tests {
    public partial class Tests {
        public void MutableString1() {
            Test_Factories();
            Test_GetHashCode();
            Test_IsAscii();
            Test_Length();
            Test_Append_Byte();
            Test_Append_Char();
            Test_Insert_Byte();
            Test_Insert_Char();
            Test_Remove_Byte();
            Test_Remove_Char();
            Test_SwitchRepr();
            Test_Concatenate();
            Test_Reverse();
        }

        public void MutableString2() {
            Test_Translate();
        }

        private MutableString/*!*/ MS(string/*!*/ data) {
            return MutableString.CreateMutable(data.Length * 3, RubyEncoding.Binary).Append(data);
        }

        private MutableString/*!*/ MS(string/*!*/ data, RubyEncoding/*!*/ e) {
            return MutableString.CreateMutable(data.Length * 3, e).Append(data);
        }

        private MutableString/*!*/ MS(byte[]/*!*/ data) {
            return MutableString.CreateBinary(data.Length * 3).Append(data);
        }

        private MutableString/*!*/ MS(byte[]/*!*/ data, RubyEncoding/*!*/ e) {
            return MutableString.CreateBinary(data.Length * 3, e).Append(data);
        }

        private void Test_Factories() {
            var x = MutableString.CreateAscii("");
            Assert(!x.IsBinary && x.IsEmpty);

            var y = MutableString.Create(x);
            Assert(!x.IsBinary && y.IsEmpty && !ReferenceEquals(x, y));

            x = MutableString.Create("a", RubyEncoding.UTF8);
            Assert(!x.IsBinary && x.ToString() == "a" && x.Encoding == RubyEncoding.UTF8);

            x = MutableString.CreateMutable(RubyEncoding.UTF8);
            Assert(!x.IsBinary && x.IsEmpty && x.Encoding == RubyEncoding.UTF8);

            x = MutableString.CreateMutable("hello", RubyEncoding.UTF8);
            Assert(!x.IsBinary && x.ToString() == "hello" && x.Encoding == RubyEncoding.UTF8);

            x = MutableString.CreateMutable(100, RubyEncoding.UTF8);
            Assert(!x.IsBinary && x.IsEmpty && x.Encoding == RubyEncoding.UTF8);

            x = MutableString.CreateBinary(new byte[] { (byte)'A' });
            Assert(x.IsBinary && x.ToString() == "A" && x.Encoding == RubyEncoding.Binary);

            x = MutableString.CreateBinary(100);
            Assert(x.IsBinary && x.IsEmpty && x.Encoding == RubyEncoding.Binary);

            x = MutableString.CreateBinary(100, RubyEncoding.UTF8);
            Assert(x.IsBinary && x.IsEmpty && x.Encoding == RubyEncoding.UTF8);

            x = MutableString.CreateBinary(new List<byte>(new byte[] { 1, 2, 3 }), RubyEncoding.UTF8);
            Assert(x.IsBinary && x.GetByteCount() == 3 && x.Encoding == RubyEncoding.UTF8);
        }

        private void Test_GetHashCode() {
            int h1, h2;
            MutableString s;

            // binary -> invalid string:
            s = MutableString.CreateBinary(new byte[] { 0xce }, RubyEncoding.UTF8);
            h1 = s.GetHashCode();
            AssertExceptionThrown<DecoderFallbackException>(() => s.GetCharCount());

            // binary -> string:
            s = MutableString.CreateBinary(new byte[] { 0xce, 0xa3 }, RubyEncoding.UTF8);
            h1 = s.GetHashCode();
            Assert(s.GetCharCount() == 1);
            h2 = s.GetHashCode();
            Assert(h1 == h2);

            // string -> binary:
            s = MutableString.Create("Σ", RubyEncoding.UTF8);
            h1 = s.GetHashCode();

            s.GetByteCount();

            h2 = s.GetHashCode();
            AssertEquals(h1, h2);

            // same content, different capacities:
            var a = MutableString.CreateMutable(10, RubyEncoding.UTF8).Append("hello");
            var b = MutableString.CreateMutable(20, RubyEncoding.UTF8).Append("hello");
            var c = MutableString.Create("hello", RubyEncoding.UTF8);
            Assert(a.GetHashCode() == b.GetHashCode());
            Assert(a.GetHashCode() == c.GetHashCode());

            // same content, different encodings:
            // ASCII characters only:
            a = MutableString.Create("hello", RubyEncoding.UTF8);
            b = MutableString.Create("hello", RubyEncoding.GetRubyEncoding("SJIS"));
            c = MutableString.CreateAscii("hello");
            Assert(a.GetHashCode() == b.GetHashCode());
            Assert(a.GetHashCode() == c.GetHashCode());
            Assert(b.GetHashCode() == c.GetHashCode());

            // non-ASCII characters:
            a = MutableString.Create("α", RubyEncoding.UTF8);
            b = MutableString.Create("α", RubyEncoding.GetRubyEncoding("SJIS"));
            c = MutableString.CreateBinary(Encoding.UTF8.GetBytes("α"), RubyEncoding.Binary);
            Assert(a.GetHashCode() != b.GetHashCode());
            Assert(a.GetHashCode() != c.GetHashCode());
            Assert(b.GetHashCode() != c.GetHashCode());

            // same content, different k-codings:
            // 1.8 doesn't know about encodings => if the strings are binary equivalent they have the same hash:
            a = MutableString.Create("hello", RubyEncoding.KCodeUTF8);
            b = MutableString.Create("hello", RubyEncoding.KCodeSJIS);
            Assert(a.GetHashCode() == b.GetHashCode());

            a = MutableString.Create("α", RubyEncoding.KCodeUTF8);
            b = MutableString.Create("α", RubyEncoding.KCodeSJIS);
            Assert(a.GetHashCode() != b.GetHashCode());
        }

        private void Test_IsAscii() {
            var a = MutableString.CreateBinary(new byte[] { 0x12, 0x34, 0x56 }, RubyEncoding.Binary);
            Assert(a.IsAscii());
            a.Remove(2);
            Assert(a.IsAscii());
            a.Append(0x56);
            Assert(a.IsAscii());
            a.Append(0x80);
            Assert(!a.IsAscii());
            a.ConvertToString();
            Assert(!a.IsAscii());
            a.Remove(2);
            Assert(a.IsAscii());
        }

        private void Test_Length() {
            MutableString x;
            x = MutableString.Create("a", RubyEncoding.Binary);
            Assert(MutableStringOps.GetLength(x) == 1);

            x = MutableString.Create("α", RubyEncoding.UTF8);
            Assert(MutableStringOps.GetLength(x) == 1);

            x = MutableString.Create("α", RubyEncoding.KCodeUTF8);
            Assert(MutableStringOps.GetLength(x) == 2);
        }

        private void Test_Append_Byte() {
            MutableString x;
            x = MutableString.CreateBinary(new byte[] { 1, 2 });
            Assert(x.GetByteCount() == 2);

            x.Append(3);
            Assert(x.GetByteCount() == 3);

            x.Append(3, 0);
            Assert(x.GetByteCount() == 3);

            x.Append(new byte[] { 4, 5 });
            Assert(x.GetByteCount() == 5);

            x.Append(new byte[] { });
            Assert(x.GetByteCount() == 5);

            x.Append(MS(new byte[] { 6 }));
            Assert(x.GetByteCount() == 6);

            x.Append(MS(new byte[] { }));
            Assert(x.GetByteCount() == 6);

            x.Append(7, 8);
            Assert(x.GetByteCount() == 14);

            x.Append(new byte[] { 14, 15, 16, 17, 18, 19, 20 }, 1, 3);
            Assert(x.GetByteCount() == 17);

            x.Append(MS(new byte[] { 14, 15, 16, 17, 18, 19, 20 }), 4, 3);
            Assert(x.GetByteCount() == 20);

            Assert(x.Equals(MS(new byte[] { 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 15, 16, 17, 18, 19, 20 })));
        }

        private void Test_Append_Char() {
            var e = RubyEncoding.UTF8;

            MutableString x;
            x = MutableString.CreateMutable(e);
            Assert(x.IsEmpty);

            x.Append("αβ");
            Assert(x.GetCharCount() == 2);

            x.Append('γ');
            Assert(x.GetCharCount() == 3);

            x.Append('x', 0);
            Assert(x.GetCharCount() == 3);

            x.Append(new char[] { 'δ', 'f' });
            Assert(x.GetCharCount() == 5);

            x.Append(new char[] { });
            Assert(x.GetCharCount() == 5);

            x.Append(MS("g", e));
            Assert(x.GetCharCount() == 6);

            x.Append(MS("", e));
            Assert(x.GetCharCount() == 6);

            x.Append('h', 8);
            Assert(x.GetCharCount() == 14);

            x.Append("hijκλμν", 1, 3);
            Assert(x.GetCharCount() == 17);

            x.Append(MS("hijκλμν", e), 4, 3);
            Assert(x.GetCharCount() == 20);

            x.Append("zzzz");
            Assert(x.GetCharCount() == 24);

            Assert(x.Equals(MS("αβγδfghhhhhhhhijκλμνzzzz", e)));
        }

        private void Test_Insert_Byte() {
            MutableString x;
            x = MutableString.CreateBinary(new byte[] { 1, 2 });
            x.Insert(0, 3);
            Assert(x.CompareTo(MS(new byte[] { 3, 1, 2 })) == 0);
            x.Insert(3, 4);
            Assert(x.CompareTo(MS(new byte[] { 3, 1, 2, 4 })) == 0);
            x.Insert(2, new byte[] { });
            Assert(x.CompareTo(MS(new byte[] { 3, 1, 2, 4 })) == 0);
            x.Insert(1, new byte[] { 5, 6, 7 });
            Assert(x.CompareTo(MS(new byte[] { 3, 5, 6, 7, 1, 2, 4 })) == 0);
            x.Insert(0, new byte[] { 8 }, 0, 1);
            Assert(x.CompareTo(MS(new byte[] { 8, 3, 5, 6, 7, 1, 2, 4 })) == 0);
            x.Insert(0, MutableString.CreateBinary(new byte[] { }));
            Assert(x.CompareTo(MS(new byte[] { 8, 3, 5, 6, 7, 1, 2, 4 })) == 0);
            x.Insert(5, MutableString.CreateBinary(new byte[] { 9, 10 }));
            Assert(x.CompareTo(MS(new byte[] { 8, 3, 5, 6, 7, 9, 10, 1, 2, 4 })) == 0);
        }

        private void Test_Insert_Char() {
            var e = RubyEncoding.UTF8;

            MutableString x;
            x = MutableString.CreateMutable("12", e);
            x.Insert(0, '3');
            Assert(x.CompareTo(MS("312", e)) == 0);
            x.Insert(3, '4');
            Assert(x.CompareTo(MS("3124", e)) == 0);
            x.Insert(2, "");
            Assert(x.CompareTo(MS("3124", e)) == 0);
            x.Insert(1, "567");
            Assert(x.CompareTo(MS("3567124", e)) == 0);
            x.Insert(0, "8", 0, 1);
            Assert(x.CompareTo(MS("83567124", e)) == 0);
            x.Insert(0, MS("", e));
            Assert(x.CompareTo(MS("83567124", e)) == 0);
            x.Insert(5, MS("9Ω", e));
            Assert(x.CompareTo(MS("835679Ω124", e)) == 0);
        }

        private void Test_Remove_Byte() {
            MutableString x;
            x = MutableString.CreateBinary(new byte[] { });
            for (int i = 0; i < 10; i++) {
                x.Append((byte)i, 10);
            }
            Assert(x.GetByteCount() == 100);
            x.Remove(20, 60);
            Assert(x.GetByteCount() == 40);
            Assert(x.GetByte(0) == 0);
            Assert(x.GetByte(10) == 1);
            Assert(x.GetByte(20) == 8);
            Assert(x.GetByte(30) == 9);

            x = MutableString.CreateBinary(new byte[] { 1, 2, 3 });
            Assert(x.Equals(MS(new byte[] { 1, 2, 3 })));
            x.Remove(0, 1);
            Assert(x.Equals(MS(new byte[] { 2, 3 })));
            x.Remove(1, 1);
            Assert(x.Equals(MS(new byte[] { 2, })));
            x.Remove(0, 1);
            Assert(x.Equals(MS(new byte[] { })));
        }

        private void Test_Remove_Char() {
            var e = RubyEncoding.UTF8;

            MutableString x;
            x = MutableString.Create("", e);
            for (int i = 0; i < 10; i++) {
                x.Append((char)('0' + i), 10);
            }
            Assert(x.GetCharCount() == 100);
            x.Remove(20, 60);
            Assert(x.GetCharCount() == 40);
            Assert(x.GetChar(0) == '0');
            Assert(x.GetChar(10) == '1');
            Assert(x.GetChar(20) == '8');
            Assert(x.GetChar(30) == '9');

            x = MutableString.Create("123", e);
            Assert(x.Equals(MS("123", e)));
            x.Remove(0, 1);
            Assert(x.Equals(MS("23", e)));
            x.Remove(1, 1);
            Assert(x.Equals(MS("2", e)));
            x.Remove(0, 1);
            Assert(x.Equals(MS("", e)));
        }

        // ASCII <-> ASCII
        // non-ASCII -> 0x80 -> '\u0080'
        public class NonInvertibleEncoding : Encoding {
            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
                for (int i = 0; i < charCount; i++) {
                    char c = chars[charIndex + i];
                    bytes[byteIndex + i] = c > '\u007f' ? (byte)0x80 : (byte)c;
                }
                return charCount;
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
                for (int i = 0; i < byteCount; i++) {
                    chars[charIndex + i] = (char)bytes[i + byteIndex];
                }
                return byteCount;
            }

            public override int GetByteCount(char[] chars, int index, int count) {
                return count;
            }

            public override int GetCharCount(byte[] bytes, int index, int count) {
                return count;
            }

            public override int GetMaxByteCount(int charCount) {
                return charCount;
            }

            public override int GetMaxCharCount(int byteCount) {
                return byteCount;
            }
        }

        private void Test_SwitchRepr() {
            // \u{12345} in UTF-8:
            var u12345 = new byte[] { 0xF0, 0x92, 0x8D, 0x85 };
            var u215c = new byte[] { 0xE2, 0x85, 0x9C };

            var surrogate = MS(u12345, RubyEncoding.UTF8);
            surrogate.Append('x');
            Assert(surrogate.GetCharCount() == 3);
            Assert(surrogate.ToString() == Encoding.UTF8.GetString(u12345) + "x");

            var result = MutableString.AppendUnicodeRepresentation(new StringBuilder(), Encoding.UTF8.GetString(u12345), false, true, -1, -1);
            Assert(result.ToString() == "\\u{12345}");

            result = MutableString.AppendUnicodeRepresentation(new StringBuilder(), Encoding.UTF8.GetString(u215c), false, true, -1, -1);
            Assert(result.ToString() == "\\u{215c}");

            //var incompleteChar = MS(new byte[] { 0xF0, 0x92 }, RubyEncoding.UTF8);
            //AssertExceptionThrown<DecoderFallbackException>(() => incompleteChar.Append('x'));

            // TODO:
            // var e = RubyEncoding.GetRubyEncoding(new NonInvertibleEncoding());
        }

        private void Test_Concatenate() {
            var utf8 = new byte[] { 0xe2, 0x85, 0x9c };
            var sjis = new byte[] { 0x82, 0xA0 };
            var ascii = new byte[] { 0x20 };

            Test_Concatenate(utf8, RubyEncoding.Binary, utf8, RubyEncoding.Binary, RubyEncoding.Binary);
            Test_Concatenate(utf8, RubyEncoding.Binary, sjis, RubyEncoding.KCodeSJIS, RubyEncoding.Binary);
            Test_Concatenate(utf8, RubyEncoding.Binary, utf8, RubyEncoding.KCodeUTF8, RubyEncoding.Binary);

            Test_Concatenate(sjis, RubyEncoding.KCodeSJIS, utf8, RubyEncoding.Binary, RubyEncoding.Binary);
            Test_Concatenate(sjis, RubyEncoding.KCodeSJIS, sjis, RubyEncoding.KCodeSJIS, RubyEncoding.KCodeSJIS);
            Test_Concatenate(sjis, RubyEncoding.KCodeSJIS, utf8, RubyEncoding.KCodeUTF8, RubyEncoding.Binary);
            Test_Concatenate(sjis, RubyEncoding.KCodeSJIS, utf8, RubyEncoding.UTF8, RubyEncoding.Binary);

            Test_Concatenate(utf8, RubyEncoding.KCodeUTF8, utf8, RubyEncoding.Binary, RubyEncoding.Binary);
            Test_Concatenate(utf8, RubyEncoding.KCodeUTF8, sjis, RubyEncoding.KCodeSJIS, RubyEncoding.Binary);
            Test_Concatenate(utf8, RubyEncoding.KCodeUTF8, utf8, RubyEncoding.KCodeUTF8, RubyEncoding.KCodeUTF8);
            Test_Concatenate(utf8, RubyEncoding.KCodeUTF8, utf8, RubyEncoding.UTF8, RubyEncoding.KCodeUTF8);

            Test_Concatenate(utf8, RubyEncoding.UTF8, sjis, RubyEncoding.KCodeSJIS, RubyEncoding.Binary);
            Test_Concatenate(utf8, RubyEncoding.UTF8, utf8, RubyEncoding.KCodeUTF8, RubyEncoding.KCodeUTF8);
            Test_Concatenate(utf8, RubyEncoding.UTF8, utf8, RubyEncoding.UTF8, RubyEncoding.UTF8);

            Test_Concatenate(utf8, RubyEncoding.UTF8, ascii, RubyEncoding.Binary, RubyEncoding.UTF8);
            Test_Concatenate(ascii, RubyEncoding.Binary, ascii, RubyEncoding.UTF8, RubyEncoding.UTF8);

            AssertExceptionThrown<EncodingCompatibilityError>(
                () => Test_Concatenate(utf8, RubyEncoding.Binary, utf8, RubyEncoding.UTF8, RubyEncoding.Binary)
            );
        }

        private void Test_Concatenate(byte[]/*!*/ b1, RubyEncoding/*!*/ e1, byte[]/*!*/ b2, RubyEncoding/*!*/ e2, RubyEncoding/*!*/ resultEncoding) {
            var s1 = MutableString.CreateBinary(b1, e1).SwitchToCharacters();
            var s2 = MutableString.CreateBinary(b2, e2).SwitchToCharacters();

            var s = MutableStringOps.Concatenate(s1, s2);
            Assert(s.Encoding == resultEncoding);
            var b = s.ToByteArray();
            Assert(b.ValueCompareTo(b.Length, ArrayUtils.AppendRange(b1, b2)) == 0);
        }

        private void Test_Reverse() {
            var SJIS = RubyEncoding.GetRubyEncoding(RubyEncoding.CodePageSJIS);
            var utf8 = new byte[] { 0xe2, 0x85, 0x9c };
            var rev_bin_utf8 = new byte[] { 0x9c, 0x85, 0xe2};
            var invalid_utf8 = new byte[] { 0xe2, 0x85, 0x9c, 0xef };
            var rev_invalid_utf8 = new byte[] { 0xef, 0xe2, 0x85, 0x9c };
            var sjis = new byte[] { 0x82, 0xA0 };
            var ascii = new byte[] { 0x20, 0x55 };
            var rev_ascii = new byte[] { 0x55, 0x20 };
            var u12345 = new byte[] { 0xF0, 0x92, 0x8D, 0x85 }; // \u{12345} in UTF-8

            Test_Reverse(new byte[0], RubyEncoding.UTF8, new byte[0]);
            Test_Reverse(utf8, RubyEncoding.UTF8, utf8);
            Test_Reverse(utf8, RubyEncoding.Binary, rev_bin_utf8);
            Test_Reverse(sjis, SJIS, sjis);
            Test_Reverse(ascii, RubyEncoding.UTF8, rev_ascii);
            
            // TODO: surrogates
            AssertExceptionThrown<EncoderFallbackException>(
                () => Test_Reverse(u12345, RubyEncoding.UTF8, u12345)
            );

            // TODO: MRI allows incorrect byte sequences
            AssertExceptionThrown<ArgumentException>(
                () => Test_Reverse(invalid_utf8, RubyEncoding.UTF8, rev_invalid_utf8)
            );

            Assert(MutableStringOps.Reverse(MutableString.Create("αΣ", RubyEncoding.UTF8)).ToString() == "Σα");

            // TODO: KCODE
        }

        private void Test_Reverse(byte[]/*!*/ b, RubyEncoding/*!*/ e, byte[]/*!*/ expected) {
            var s = MutableString.CreateBinary(b, e);
            MutableStringOps.Reverse(s);
            var actual = s.ToByteArray();
            Assert(actual.ValueEquals(expected));
        }

        private byte[] Utf8(string str) {
            return Encoding.UTF8.GetBytes(str);
        }

        private byte[] Sjis(string str) {
            return Encoding.GetEncoding("SJIS").GetBytes(str);
        }

        private void Test_Translate() {
            var SJIS = RubyEncoding.GetRubyEncoding(RubyEncoding.CodePageSJIS);
            var sjis = new byte[] { 0x82, 0xA0 };
            var u12345 = new byte[] { 0xF0, 0x92, 0x8D, 0x85 }; // \u{12345} in UTF-8

            Test_Translate(
                Utf8("αβγδ"), RubyEncoding.UTF8,
                Utf8("α-γ"), RubyEncoding.UTF8, 
                Utf8("AB"), SJIS,
                Utf8("ABBδ"), RubyEncoding.UTF8
            );

            Test_Translate(
                Utf8("αaβzγcδ"), RubyEncoding.UTF8,
                Utf8("a-z"), RubyEncoding.Binary,
                Utf8("*"), SJIS,
                Utf8("α*β*γ*δ"), RubyEncoding.UTF8
            );

            Test_Translate(
                Utf8("αaβzγcδ"), RubyEncoding.UTF8,
                Utf8("^α-δ"), RubyEncoding.UTF8,
                Utf8("-"), SJIS,
                Utf8("α-β-γ-δ"), RubyEncoding.UTF8
            );
            
            Test_Translate(
                Utf8("-α-"), RubyEncoding.Binary,
                Utf8("α"), RubyEncoding.Binary,
                Utf8("AB"), RubyEncoding.Binary,
                Utf8("-AB-"), RubyEncoding.Binary
            );

            Test_Translate(
                Utf8("-a-"), SJIS,
                Utf8("a"), RubyEncoding.Binary,
                Utf8("A"), RubyEncoding.UTF8,
                Utf8("-A-"), SJIS
            );

            Test_Translate(
               Utf8("a"), RubyEncoding.UTF8,
               Utf8("a"), RubyEncoding.UTF8,
               Utf8("\0"), RubyEncoding.UTF8,
               Utf8("\0"), RubyEncoding.UTF8
           );

            AssertExceptionThrown<EncodingCompatibilityError>(
                () => Test_Translate(Utf8("α"), RubyEncoding.Binary, Utf8("α"), RubyEncoding.UTF8, Utf8("-"), SJIS, null, null)
            );

            AssertExceptionThrown<EncodingCompatibilityError>(
                () => Test_Translate(Utf8("α"), RubyEncoding.UTF8, Sjis("ﾎ"), SJIS, Utf8("-"), SJIS, null, null)
            );

            // TODO: KCODE

        }

        private void Test_Translate(
            byte[]/*!*/ bself, RubyEncoding/*!*/ eself,
            byte[]/*!*/ bfrom, RubyEncoding/*!*/ efrom,
            byte[]/*!*/ bto, RubyEncoding/*!*/ eto, 
            byte[]/*!*/ expected, RubyEncoding/*!*/ expectedEncoding) {

            var self = MutableString.CreateBinary(bself, eself);
            var from = MutableString.CreateBinary(bfrom, efrom);
            var to = MutableString.CreateBinary(bto, eto);

            var result = MutableStringOps.GetTranslated(self, from, to);
            Assert(result.Encoding == expectedEncoding);
            var b = result.ToByteArray();
            Assert(b.ValueEquals(expected));
        }
    }
}
