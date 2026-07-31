namespace TbcaTest.CrossCutting.Security;

public static class PersonalDataMasker
{
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "(missing)";
        }

        var parts = email.Split('@', 2);
        var prefix = parts[0].Length <= 2 ? parts[0][0] + "*" : parts[0][..2] + "***";
        return $"{prefix}@{parts[1]}";
    }
}


