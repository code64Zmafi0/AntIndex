namespace AntIndex.Services.Search;

public class Perfomancer(int quantity)
{
    private int MatchesCount = 0;

    public void IncrementMatch()
        => MatchesCount++;

    public bool NeedContinue
        => MatchesCount <= quantity;
}
