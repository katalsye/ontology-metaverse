using Firebase.Firestore;
using System.Collections.Generic;

[FirestoreData]
public class Reward
{
    [FirestoreProperty]
    public int Amount { get; set; }

    [FirestoreProperty]
    public string RewardType { get; set; } // 재화 / 아이템

    [FirestoreProperty]
    public List<string> Items { get; set; }
}