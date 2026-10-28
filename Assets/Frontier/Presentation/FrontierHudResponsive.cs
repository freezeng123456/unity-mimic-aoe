using System;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        Font cjkFont;
        RectTransform actionViewport;
        ScrollRect actionScroll;
        Vector2 previousCanvasSize;
        Vector2 CanvasSize
        {
            get{float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f)*(game.Options?.uiScale??1);return new Vector2(Screen.width,Screen.height)/Mathf.Max(.01f,scale);}
        }
        float ActionWidth=>actionViewport!=null?actionViewport.sizeDelta.x:807;
        int ActionColumns=>Mathf.Clamp(Mathf.FloorToInt(ActionWidth/155),2,5);
        public void ApplyPreferences()
        {
            if(canvas==null)return;
            canvas.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1600,900)/(game.Options?.uiScale??1);
            previousCanvasSize=CanvasSize;MarkUiLayoutDirty();InvalidateActions();
            if(menuPanel!=null)LayoutMenu();if(gamePanel!=null)LayoutGame();
            if(modal!=null)foreach(Transform child in modal)if(child is RectTransform rt)CenterModal(rt,rt.sizeDelta.y);
        }
        void UpdateResponsiveLayout()
        {if((CanvasSize-previousCanvasSize).sqrMagnitude>.5f)ApplyPreferences();}
        void CenterModal(RectTransform box,float height)
        {
            var size=CanvasSize;float fit=Mathf.Min(1,(size.x-40)/870,(size.y-35)/height);
            box.anchorMin=box.anchorMax=box.pivot=new Vector2(.5f,1);box.anchoredPosition=new Vector2(0,-Mathf.Min(95,Mathf.Max(17.5f,(size.y-height*fit)*.5f)));box.sizeDelta=new Vector2(870,height);box.localScale=Vector3.one*fit;
        }
        void CreateActionViewport(Transform bottom)
        {
            actionViewport=Rect(bottom,"Context action viewport",new Rect(757,14,807,180));
            actionViewport.gameObject.AddComponent<RectMask2D>();
            actionScroll=actionViewport.gameObject.AddComponent<ScrollRect>();actionScroll.horizontal=false;actionScroll.vertical=true;
            actionScroll.movementType=ScrollRect.MovementType.Clamped;actionScroll.scrollSensitivity=30;actionScroll.viewport=actionViewport;
            actionPanel=Rect(actionViewport,"Context actions",new Rect(0,0,807,180));actionScroll.content=(RectTransform)actionPanel;
        }
        void ResizeActionContent(int count)
        {if(actionPanel is RectTransform rt)rt.sizeDelta=new Vector2(ActionWidth,Mathf.Max(180,Mathf.Ceil(count/(float)ActionColumns)*57));}
        static void Position(RectTransform rt,float x,float y,float width,float height)
        {rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(width,height);}
        void LayoutGame()
        {
            var size=CanvasSize;float width=size.x;
            var top=(RectTransform)gamePanel.Find("Resource and age bar");top.anchorMin=new Vector2(0,1);top.anchorMax=new Vector2(1,1);top.anchoredPosition=Vector2.zero;top.sizeDelta=new Vector2(0,64);
            Position(resources.rectTransform,83,14,width-490,40);resources.resizeTextForBestFit=true;resources.resizeTextMinSize=16;resources.resizeTextMaxSize=22;
            Position(clockLabel.rectTransform,width-408,19,150,30);
            foreach(var b in top.GetComponentsInChildren<Button>())
            {
                var label=b.GetComponentInChildren<Text>();if(label.text.StartsWith("HOME CITY")){label.text="HOME CITY ["+game.Settings.BindingLabel(PlayerAction.HomeCity)+"]";Position((RectTransform)b.transform,width-248,10,162,43);}
                else Position((RectTransform)b.transform,width-75,10,57,43);
            }
            var bottom=(RectTransform)gamePanel.Find("Orders and economy");bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=Vector2.zero;bottom.anchoredPosition=new Vector2(10,10);bottom.sizeDelta=new Vector2(-20,208);
            float detailWidth=Mathf.Clamp(530-(1600-width)*.4f,300,530),actionsX=213+detailWidth+14;
            Position(selection.rectTransform,213,14,detailWidth,30);selection.resizeTextForBestFit=true;selection.resizeTextMinSize=16;selection.resizeTextMaxSize=23;
            Position(details.rectTransform,213,50,detailWidth-15,90);
            Position((RectTransform)queuePanel,213,145,530,48);queuePanel.localScale=Vector3.one*Mathf.Min(1,detailWidth/530);
            Position(actionViewport,actionsX,14,width-20-actionsX-16,180);ResizeActionContent(actionPanel.childCount);
            if(captionLabel!=null){Position(captionLabel.rectTransform,width*.61f,146,width*.37f,100);captionLabel.resizeTextForBestFit=true;captionLabel.resizeTextMinSize=14;captionLabel.resizeTextMaxSize=18;}
            var cameraHelp=gamePanel.Find("Camera help").GetComponent<Text>();
            Position(cameraHelp.rectTransform,width*.61f,78,width*.37f,60);cameraHelp.fontSize=14;
            cameraHelp.text="CAMERA: "+game.Settings.BindingLabel(PlayerAction.CameraLeft)+" / "+game.Settings.BindingLabel(PlayerAction.CameraRight)+" / "+game.Settings.BindingLabel(PlayerAction.CameraUp)+" / "+game.Settings.BindingLabel(PlayerAction.CameraDown)+" or window edge\nMINIMAP: left to view / right to move";
            if(game.IsWatching)cameraHelp.text="VIEWING CAMERA: arrow keys / window edge\nMINIMAP: left to view · recorded commands are read-only";
            LayoutReplayHud(width);
            if(guideLabel!=null)
            {
                float guideWidth=Mathf.Min(710,width*.57f);((RectTransform)guideLabel.transform.parent).sizeDelta=new Vector2(guideWidth,116);guideLabel.rectTransform.sizeDelta=new Vector2(guideWidth-25,96);
            }
            if(orders!=null)orders.rectTransform.sizeDelta=new Vector2(Mathf.Min(610,width-40),32);
            var eventPanel=notice.transform.parent as RectTransform;if(eventPanel!=null)eventPanel.sizeDelta=new Vector2(width-444,38);notice.rectTransform.sizeDelta=new Vector2(width-468,27);
            if(help!=null)help.rectTransform.sizeDelta=new Vector2(width-444,72);
        }
        void LayoutMenu()
        {
            if(game.ScenarioEditorOpen&&menuPanel.name=="Scenario workshop"){LayoutScenarioEditor069();return;}
            var size=CanvasSize;
            var config=menuPanel.Find("Skirmish configuration") as RectTransform;if(config==null)return;
            float scale=Mathf.Min(1,(size.y-80)/710);config.localScale=Vector3.one*scale;Position(config,size.x-510*scale-35,40,510,710);
            float leftWidth=config.anchoredPosition.x-90;
            int expansion=0;
            foreach(Transform child in menuPanel)
            {
                if(child==config)continue;
                var text=child.GetComponent<Text>();var button=child.GetComponent<Button>();var rt=(RectTransform)child;
                if(button!=null)
                {
                    var label=child.GetComponentInChildren<Text>();string title=label.text;
                    float third=(leftWidth-20)/3;
                    if(title=="LOAD SAVED MATCH")Position(rt,45,474,third,48);
                    else if(title=="SCENARIO WORKSHOP")Position(rt,45+third+10,474,third,48);
                    else if(title=="CLASSIC CIVILIZATIONS")Position(rt,45+2*(third+10),474,third,48);
                    else if(title=="MAP, SEED & OBJECTIVES")Position(rt,45,538,leftWidth,48);
                    else {float cell=(leftWidth+10)/3;Position(rt,45+(expansion%3)*cell,338+(expansion/3)*65,cell-10,52);expansion++;}
                    Position(label.rectTransform,10,4,rt.sizeDelta.x-20,rt.sizeDelta.y-8);
                    label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=15;
                }
                else if(text!=null)
                {
                    if(text.text.StartsWith("FRONTIER")){Position(rt,45,32,leftWidth,140);text.fontSize=46;}
                    else if(text.text=="A LAND OF CHOICES"){Position(rt,45,188,leftWidth,35);text.fontSize=20;}
                    else if(text.text.StartsWith("Build an economy")){Position(rt,45,243,leftWidth,75);text.fontSize=19;}
                    else if(text.text=="FIVE-ERA FRONTIER"){Position(rt,45,Mathf.Min(610,size.y-96),leftWidth,25);text.fontSize=17;}
                    else {Position(rt,45,Mathf.Min(645,size.y-66),leftWidth,61);text.fontSize=13;}
                }
            }
        }
    }
}
