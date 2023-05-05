using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class RestaurantGameUI : MonoBehaviour
{
    public UIDocument UIDocument;

    public Label Label_CustomersInLine;
    public Label Label_PendingOrders;
    public Label Label_KitchenDirtiness;
    public Label Label_CleaningSupplies;
    public Label Label_CompletedOrders;

    public ListView List_IdleWorkers;
    public ListView List_ServiceWorkers;
    public ListView List_CookWorkers;
    public ListView List_CleanWorkers;

    public List<string> IdleBackingList = new List<string>();
    public List<string> ServiceBackingList = new List<string>();
    public List<string> CookBackingList = new List<string>();
    public List<string> CleanBackingList = new List<string>();

    private void Start()
    {
        Label_CustomersInLine = UIDocument.rootVisualElement.Q("CustomersInLine").Q<Label>("Value");
        Label_PendingOrders = UIDocument.rootVisualElement.Q("PendingOrders").Q<Label>("Value");
        Label_KitchenDirtiness = UIDocument.rootVisualElement.Q("KitchenDirtiness").Q<Label>("Value");
        Label_CleaningSupplies = UIDocument.rootVisualElement.Q("CleaningSupplies").Q<Label>("Value");
        Label_CompletedOrders = UIDocument.rootVisualElement.Q("CompletedOrders").Q<Label>("Value");

        List_IdleWorkers = UIDocument.rootVisualElement.Q("IdleColumn").Q<ListView>("ListView");
        List_IdleWorkers.itemsSource = IdleBackingList;
        List_IdleWorkers.makeItem = MakeWorkerStateListItem;
        List_IdleWorkers.bindItem = (e, i) =>
        {
            (e as Label).text = IdleBackingList[i];
        };

        List_ServiceWorkers = UIDocument.rootVisualElement.Q("ServiceColumn").Q<ListView>("ListView");
        List_ServiceWorkers.itemsSource = ServiceBackingList;
        List_ServiceWorkers.makeItem = MakeWorkerStateListItem;
        List_ServiceWorkers.bindItem = (e, i) =>
        {
            (e as Label).text = ServiceBackingList[i];
        };

        List_CookWorkers = UIDocument.rootVisualElement.Q("CookColumn").Q<ListView>("ListView");
        List_CookWorkers.itemsSource = CookBackingList;
        List_CookWorkers.makeItem = MakeWorkerStateListItem;
        List_CookWorkers.bindItem = (e, i) =>
        {
            (e as Label).text = CookBackingList[i];
        };

        List_CleanWorkers = UIDocument.rootVisualElement.Q("CleanColumn").Q<ListView>("ListView");
        List_CleanWorkers.itemsSource = CleanBackingList;
        List_CleanWorkers.makeItem = MakeWorkerStateListItem;
        List_CleanWorkers.bindItem = (e, i) =>
        {
            (e as Label).text = CleanBackingList[i];
        };

        World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<RestaurantGameUISystem>().GameUI = this;
    }

    private VisualElement MakeWorkerStateListItem()
    {
        Label label = new Label();
        label.style.color = Color.white;
        return label;
    }
}
