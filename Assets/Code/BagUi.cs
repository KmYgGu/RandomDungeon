using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BagUi : MonoBehaviour
{
    public GameObject panel;

    public GameObject BagPanel;
    public TextMeshProUGUI BagTextMeshPro;
    public TextMeshProUGUI BagCusor;

    public GameObject itemChosePanel;
    public TextMeshProUGUI itemChoseText;

    public GameObject ExplainitemPanel;
    public Image Explainimage;
    public TextMeshProUGUI Showitemname;
    public TextMeshProUGUI Showitemprice;
    public TextMeshProUGUI ShowexplainText;

    public void bagTextBox(GameObject MainPanel, GameObject _BagPanel)
    {
        MainPanel.SetActive(false);
        _BagPanel.SetActive(true);
    }
}
