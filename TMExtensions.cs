using System.Text;
using ProjectM.Network;
using Unity.Entities;

namespace troublemaker;

public static class TMExtensions
{
    public static Entity GetEntity(this User user)
    {
        return user.LocalCharacter._Entity;
    }

    /// <summary>
    /// The <c>Pop()</c> method removes the last element from an array and returns that element. This method changes the length of the array.
    /// </summary>
    public static T Pop<T>(this System.Collections.Generic.List<T> self)
    {
        T r = self[self.Count-1];
        self.RemoveAt(self.Count-1);
        return r;
    }

    /// <summary>
    /// The <c>Shift()</c> method removes the first element from an array and returns that element. This method changes the length of the array.
    /// </summary>
    public static T Shift<T>(this System.Collections.Generic.List<T> self)
    {
        T r = self[0];
        self.RemoveAt(0);
        return r;
    }
}

internal enum BloodTypes
{
    Creature = -1418430647,
    Warrior = 923140362,
    Rogue = 203103783, 
    Brute = -1464869978,
    Scholar = 1406393857,
    Worker = -2025921616
}

internal struct FakeNull
{
    public int value;
    public bool has_value;
}