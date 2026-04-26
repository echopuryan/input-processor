using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services;

/// <summary>
/// Processes the user input by sorting the characters in the input string by ascending order of their occurrence in the input string.
/// </summary>
public class InputProcessingService : IInputProcessingService
{
    /// <summary>
    /// Process the input string by sorting the characters in the input string by ascending order of their occurrence in the input string.
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>SortedString/base64 string</returns>
    public Task<string> ProcessInputAsync(string input, CancellationToken cancellationToken)
    {
        var inputCharacterCounts = new Dictionary<char, int>();

        // check if the input is null or empty
        if (string.IsNullOrEmpty(input)) throw new ArgumentException("Input cannot be null or empty", nameof(input));

        // Convert the input string to a base64 encoded string
        var base64EncodedInput = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

        foreach (var c in input)
        {
            // update the character count in the dictionary if it already exists, otherwise add it to the dictionary with a count of 1
            if (inputCharacterCounts.ContainsKey(c))
                inputCharacterCounts[c]++;
            else
                inputCharacterCounts[c] = 1;
            
            // NOTE: This is likely not needed in this loop as string sorting is very fast.
            cancellationToken.ThrowIfCancellationRequested();
        }

        // sort the dictionary by the character count in ascending order and then by the character itself in ascending order
        var sortedCharacters = inputCharacterCounts.OrderBy(c => c.Value).ThenBy(c => c.Key).ToList();

        var responseBuilder = new StringBuilder();
        foreach (var c in sortedCharacters)
        {
            responseBuilder.Append($"{c.Key}{c.Value}");
        }

        // append the rest of the response string
        responseBuilder.Append($"/{base64EncodedInput}");

        return Task.FromResult(responseBuilder.ToString());
    }
}
