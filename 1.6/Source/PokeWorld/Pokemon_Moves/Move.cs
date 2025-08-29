namespace PokeWorld;

public class Move
{
    public MoveDef moveDef;
    public int unlockLevel = 1;
    public MoveLearnMethod learnMethod = MoveLearnMethod.Level | MoveLearnMethod.Tutor;
}
