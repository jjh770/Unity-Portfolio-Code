using System;
using Firebase.Firestore;

[Serializable]
[FirestoreData]
public sealed class SlimeInstanceSaveData
{
    [FirestoreProperty]
    public string InstanceId { get; set; }

    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public bool IsSpecial { get; set; }

    [FirestoreProperty]
    public int Location { get; set; }

    public SlimeInstanceSaveData() { }

    public SlimeInstanceSaveData(
        string instanceId,
        ESlimeGrade grade,
        bool isSpecial,
        ESlimeLocation location)
    {
        InstanceId = instanceId;
        Grade = (int)grade;
        IsSpecial = isSpecial;
        Location = (int)location;
    }

    public SlimeInstance ToDomain()
    {
        return new SlimeInstance(
            InstanceId,
            (ESlimeGrade)Grade,
            IsSpecial,
            (ESlimeLocation)Location);
    }

    public static SlimeInstanceSaveData FromDomain(SlimeInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        return new SlimeInstanceSaveData(
            instance.InstanceId,
            instance.Grade,
            instance.IsSpecial,
            instance.Location);
    }
}
