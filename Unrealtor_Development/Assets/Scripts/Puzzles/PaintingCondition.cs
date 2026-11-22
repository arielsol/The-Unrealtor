using UnityEngine;

public class PaintingCondition : PuzzleBase
{
    bool IsLeftSideCorrect =  false;
    bool IsRightSideCorrect = false;

    public void SetPaintingSides(bool isLeftSide, bool isCorrect)
    {
        if(isLeftSide) IsLeftSideCorrect = isCorrect;
        else IsRightSideCorrect = isCorrect;

        isSolved = (IsLeftSideCorrect && IsRightSideCorrect); 
    }
    
}
