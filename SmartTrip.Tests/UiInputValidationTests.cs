using System;
using System.Collections.Generic;
using Xunit;

namespace SmartTrip.Tests.Utilities;

public static class UiInputValidationTests
{
    public class MoneyValidation
    {
        [Theory]
        [InlineData(100, true)]
        [InlineData(1500.50, true)]
        [InlineData(0.99, true)]
        [InlineData(0, true)]
        public void ShouldValidateMoneyAmountsValid(decimal amount, bool expected)
        {
            bool isValid = amount >= 0 && amount <= 100000000;
            Assert.Equal(expected, isValid);
        }

        [Theory]
        [InlineData(-50, false)]
        [InlineData(-0.01, false)]
        public void ShouldValidateMoneyAmountsNegative(decimal amount, bool expected)
        {
            bool isValid = amount >= 0;
            Assert.Equal(expected, isValid);
        }

        [Fact]
        public void ShouldHandleZeroAmount()
        {
            decimal amount = 0;
            Assert.Equal(0, amount);
            Assert.True(amount >= 0);
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(1.999)]
        public void ShouldHandleDecimalPrecision(decimal amount)
        {
            decimal rounded = decimal.Round(amount, 2);
            Assert.True(rounded >= 0);
        }
    }

    public class IntegerValidation
    {
        [Theory]
        [InlineData(1, true)]
        [InlineData(100, true)]
        [InlineData(0, true)]
        public void ShouldAcceptValidIntegers(int value, bool expected)
        {
            bool isValid = value >= 0;
            Assert.Equal(expected, isValid);
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(-100, false)]
        public void ShouldRejectNegativeIntegers(int value, bool expected)
        {
            bool isValid = value >= 0;
            Assert.Equal(expected, isValid);
        }
    }

    public class StringValidation
    {
        [Theory]
        [InlineData("Valid Text", false)]
        [InlineData("Another valid string", false)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        public void ShouldValidateStrings(string value, bool isEmpty)
        {
            bool result = string.IsNullOrWhiteSpace(value);
            Assert.Equal(isEmpty, result);
        }

        [Theory]
        [InlineData("Категорія", 100, true)]
        [InlineData("A", 100, true)]
        public void ShouldCheckStringLength(string value, int maxLength, bool isValid)
        {
            bool result = value.Length <= maxLength;
            Assert.Equal(isValid, result);
        }
    }

    public class MoneyParsing
    {
        [Theory]
        [InlineData("100", 100)]
        [InlineData("1500.50", 1500.50)]
        [InlineData("0.99", 0.99)]
        public void ShouldParseValidMoneyStrings(string input, decimal expected)
        {
            bool parsed = decimal.TryParse(
                input.Replace(',', '.'),
                out decimal result);

            Assert.True(parsed);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("abc123")]
        [InlineData("10.50.5")]
        public void ShouldFailToParseInvalidMoneyStrings(string input)
        {
            bool parsed = decimal.TryParse(
                input.Replace(',', '.'),
                out _);

            Assert.False(parsed);
        }
    }

    public class IntegerParsing
    {
        [Theory]
        [InlineData("1", 1)]
        [InlineData("100", 100)]
        [InlineData("0", 0)]
        public void ShouldParseValidIntegerStrings(string input, int expected)
        {
            bool parsed = int.TryParse(input, out int result);
            Assert.True(parsed);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("12.5")]
        [InlineData("abc")]
        public void ShouldFailToParseInvalidIntegerStrings(string input)
        {
            bool parsed = int.TryParse(input, out _);
            Assert.False(parsed);
        }
    }
}