using UnityEngine;
using UnityEngine.UI;
using Yarn.Markup;
using Yarn.Unity.Attributes;
using Yarn.Unity;
using System.Collections.Generic;
using System.Threading;
using TMPro;


public class DialogueUISwitch : DialoguePresenterBase
{
    public GameObject mayaUI;
    public GameObject noahUI;
    public GameObject edgarUI;
    public GameObject noneUI;

    public Color mayaColor;
    public Color noahColor;
    public Color edgarColor;

    public TextMeshProUGUI charText;
    public TextMeshProUGUI txt;

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        mayaUI.SetActive(false);
        noahUI.SetActive(false);
        edgarUI.SetActive(false);
        noneUI.SetActive(false);

        switch (line.CharacterName)
        {
            case "Maya":
                mayaUI.SetActive(true);
                charText.color = mayaColor;
                txt.color = mayaColor;
                break;
            case "Noah":
                noahUI.SetActive(true);
                charText.color = noahColor;
                txt.color = noahColor;
                break;
            case "Edgar":
                edgarUI.SetActive(true);
                charText.color = edgarColor;
                txt.color = edgarColor;
                break;
            case null:
                noneUI.SetActive(true);
                charText.color = new Color32(255, 255, 255, 255);
                txt.color = new Color32(255, 255, 255, 255);
                break;
        }
    }

    /// <inheritdoc/>
    public override YarnTask OnDialogueCompleteAsync()
    {
        return YarnTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override YarnTask OnDialogueStartedAsync()
    {
        return YarnTask.CompletedTask;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, CancellationToken cancellationToken)
    {
        return YarnTask<DialogueOption?>.FromResult(null);
    }
}
