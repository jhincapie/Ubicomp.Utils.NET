using System;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Implements a sliding window replay protection mechanism.
    /// Uses a 64-bit mask to track received sequence IDs relative to the highest seen.
    /// </summary>
    internal class ReplayWindow
    {
        // Window size matches the bitmask size (ulong = 64 bits)
        private const int WindowSize = 64;

        private int _highestSequenceId = -1;
        private ulong _windowMask = 0; // Bit 0 corresponds to _highestSequenceId
        private readonly object _lock = new object(); // Simple lock for thread safety per-source

        public ReplayWindow()
        {
        }

        /// <summary>
        /// Checks if a sequence ID is valid (not a replay and not too old).
        /// Marks it as received if valid.
        /// </summary>
        /// <param name="sequenceId">The incoming sequence ID.</param>
        /// <returns>True if the message should be accepted; False if it's a replay or too old.</returns>
        public bool CheckAndMark(int sequenceId)
        {
            lock (_lock)
            {
                // Case 1: First packet ever
                if (_highestSequenceId == -1)
                {
                    _highestSequenceId = sequenceId;
                    _windowMask = 1; // Mark bit 0
                    return true;
                }

                // Case 2: New highest sequence ID
                if (sequenceId > _highestSequenceId)
                {
                    int diff = sequenceId - _highestSequenceId;

                    if (diff >= WindowSize)
                    {
                        // Jumped way ahead (gap larger than window), clear everything
                        _windowMask = 0;
                    }
                    else
                    {
                        // Shift the window to the left to make room
                        _windowMask <<= diff;
                    }

                    _highestSequenceId = sequenceId;
                    _windowMask |= 1; // Mark the new highest (bit 0)
                    return true;
                }

                // Case 3: Old sequence ID
                int offset = _highestSequenceId - sequenceId;

                // If offset is negative, it means sequenceId > highest, handled above.
                // So offset >= 0 here.

                if (offset >= WindowSize)
                {
                    // Too old, outside the window
                    return false;
                }

                // Case 4: Within window, check for duplicates
                ulong maskPosition = 1UL << offset;

                if ((_windowMask & maskPosition) != 0)
                {
                    // Already set, this is a replay
                    return false;
                }

                // Not duplicate, mark it
                _windowMask |= maskPosition;
                return true;
            }
        }
    }
}
