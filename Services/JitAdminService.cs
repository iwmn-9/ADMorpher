using System;
using System.Security.Cryptography;
using System.Text;
using ADMorpher.Models;

namespace ADMorpher.Services
{
    public class JitAdminService
    {
        private const string CharsUpper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // I, O除外
        private const string CharsLower = "abcdefghijkmnopqrstuvwxyz"; // l除外
        private const string CharsDigits = "23456789"; // 0, 1除外
        private const string CharsSymbols = "!@#$%^&*()_-+=?";

        /// <summary>
        /// 暗号学的に安全なJITワンタイム管理者パスワードを生成
        /// </summary>
        public string GenerateJitPassword(int length = 18)
        {
            if (length < 12) length = 12;

            var chars = new StringBuilder();
            // 各文字種を最低1文字保証
            chars.Append(GetRandomChar(CharsUpper));
            chars.Append(GetRandomChar(CharsLower));
            chars.Append(GetRandomChar(CharsDigits));
            chars.Append(GetRandomChar(CharsSymbols));

            string allChars = CharsUpper + CharsLower + CharsDigits + CharsSymbols;
            for (int i = 4; i < length; i++)
            {
                chars.Append(GetRandomChar(allChars));
            }

            // Fisher-Yates シャッフル
            return ShuffleString(chars.ToString());
        }

        /// <summary>
        /// LAPS有効期限を即座にリセット（次回ログオン時またはバックグラウンド通信時に強制再ローテーション）
        /// </summary>
        public void ForceImmediateRotation(JitDevice device)
        {
            // 有効期限を現在時刻に設定
            device.PasswordExpiration = DateTime.Now;
            // シミュレーション/即時更新: 新たなワンタイムパスワードを再割り当て
            device.CurrentLapsPassword = GenerateJitPassword(18);
            device.IsMasked = true;
        }

        private static char GetRandomChar(string source)
        {
            int index = RandomNumberGenerator.GetInt32(source.Length);
            return source[index];
        }

        private static string ShuffleString(string input)
        {
            char[] array = input.ToCharArray();
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = RandomNumberGenerator.GetInt32(n + 1);
                (array[k], array[n]) = (array[n], array[k]);
            }
            return new string(array);
        }
    }
}
