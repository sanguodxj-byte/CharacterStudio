using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_Glossary : Window
    {
        private Vector2 scrollPos;
        private string searchText = "";

        // 术语表数据结构
        private class TermEntry
        {
            public string Term = "";
            public string Definition = "";
            public string Category = "";
        }

        private List<TermEntry> allTerms = new List<TermEntry>();
        private List<TermEntry> filteredTerms = new List<TermEntry>();

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public Dialog_Glossary()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;
            this.absorbInputAroundWindow = false;

            InitializeTerms();
            filteredTerms = new List<TermEntry>(allTerms);
        }

        private void InitializeTerms()
        {
            // 初始化术语表，这里使用翻译键
            // 在实际使用中，这可以从 XML 或其他数据源加载
            allTerms = new List<TermEntry>
            {
                new TermEntry { 
                    Term = "CS_Glossary_Term_Anchor".Translate(), 
                    Definition = "CS_Glossary_Def_Anchor".Translate(), 
                    Category = "CS_Glossary_Cat_Rendering".Translate() 
                },
                new TermEntry { 
                    Term = "CS_Glossary_Term_RenderNode".Translate(), 
                    Definition = "CS_Glossary_Def_RenderNode".Translate(), 
                    Category = "CS_Glossary_Cat_Rendering".Translate() 
                },
                new TermEntry { 
                    Term = "CS_Glossary_Term_PawnLayer".Translate(), 
                    Definition = "CS_Glossary_Def_PawnLayer".Translate(), 
                    Category = "CS_Glossary_Cat_Core".Translate() 
                },
                new TermEntry { 
                    Term = "CS_Glossary_Term_AbilityType".Translate(), 
                    Definition = "CS_Glossary_Def_AbilityType".Translate(), 
                    Category = "CS_Glossary_Cat_Ability".Translate() 
                },
                new TermEntry { 
                    Term = "CS_Glossary_Term_ModularAbility".Translate(), 
                    Definition = "CS_Glossary_Def_ModularAbility".Translate(), 
                    Category = "CS_Glossary_Cat_Ability".Translate() 
                }
            };
        }

        private void FilterTerms()
        {
            if (string.IsNullOrEmpty(searchText))
            {
                filteredTerms = new List<TermEntry>(allTerms);
                return;
            }

            string searchLower = searchText.ToLower();
            filteredTerms = allTerms.FindAll(t => 
                t.Term.ToLower().Contains(searchLower) || 
                t.Definition.ToLower().Contains(searchLower) ||
                t.Category.ToLower().Contains(searchLower)
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Glossary_Title".Translate());
            Text.Font = GameFont.Small;

            float y = 40;

            // 搜索栏
            string newSearch = Widgets.TextEntryLabeled(new Rect(0, y, inRect.width, 24), "CS_Studio_Browser_Search".Translate(), searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                FilterTerms();
            }
            y += 35;

            // 列表区域
            Rect listRect = new Rect(0, y, inRect.width, inRect.height - y - 10);
            Rect viewRect = new Rect(0, 0, listRect.width - 16, CalculateListHeight(listRect.width - 16));

            Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);

            float curY = 0;
            string lastCategory = "";

            foreach (var term in filteredTerms)
            {
                // 分类标题
                if (term.Category != lastCategory)
                {
                    lastCategory = term.Category;
                    UIHelper.DrawSectionTitle(ref curY, viewRect.width, lastCategory);
                }

                // 术语条目
                float termHeight = Text.CalcHeight(term.Definition, viewRect.width - 20) + 24;
                Rect termRect = new Rect(10, curY, viewRect.width - 20, termHeight);
                
                // 背景高亮
                Widgets.DrawHighlightIfMouseover(termRect);
                
                // 术语名称
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10, curY, termRect.width, 24), term.Term);
                
                // 定义内容
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10, curY + 20, termRect.width, termHeight - 24), term.Definition);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                curY += termHeight + 5;
            }

            Widgets.EndScrollView();
        }

        private float CalculateListHeight(float width)
        {
            float height = 0;
            string lastCategory = "";
            foreach (var term in filteredTerms)
            {
                if (term.Category != lastCategory)
                {
                    lastCategory = term.Category;
                    height += 24; // Section Title
                }
                height += Text.CalcHeight(term.Definition, width - 20) + 24 + 5;
            }
            return height;
        }
    }
}
