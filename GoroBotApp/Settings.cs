using System;
using System.Collections.Generic;
using System.Text;

namespace GoroBotApp
{
    internal static class Settings
    {
        public static string AccessToken
        {
            get => GetEnvironmentVariable("AccessToken");
        }

        public static string ChannelSecret
        {
            get => GetEnvironmentVariable("ChannelSecret");
        }

        private static string GetEnvironmentVariable(string variable)
        {
            var result = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process);
            if (String.IsNullOrEmpty(result))
            {
                throw new Exception($"{variable} is null or empty.");
            }
            return result;
        }
    }
}
