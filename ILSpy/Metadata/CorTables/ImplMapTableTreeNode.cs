﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using Mono.Cecil;

namespace ICSharpCode.ILSpy.Metadata
{
	class ImplMapTableTreeNode : ILSpyTreeNode
	{
		private PEFile module;

		public ImplMapTableTreeNode(PEFile module)
		{
			this.module = module;
		}

		public override object Text => $"1C ImplMap ({module.Metadata.GetTableRowCount(TableIndex.ImplMap)})";

		public override object Icon => Images.Literal;

		public unsafe override bool View(ViewModels.TabPageModel tabPage)
		{
			tabPage.Title = Text.ToString();
			tabPage.SupportsLanguageSwitching = false;

			var view = Helpers.PrepareDataGrid(tabPage);
			var metadata = module.Metadata;

			var list = new List<ImplMapEntry>();

			var length = metadata.GetTableRowCount(TableIndex.ImplMap);
			byte* ptr = metadata.MetadataPointer;
			int metadataOffset = module.Reader.PEHeaders.MetadataStartOffset;
			for (int rid = 1; rid <= length; rid++) {
				list.Add(new ImplMapEntry(module, ptr, metadataOffset, rid));
			}

			view.ItemsSource = list;

			tabPage.Content = view;
			return true;
		}

		readonly struct ImplMap
		{
			public readonly PInvokeAttributes MappingFlags;
			public readonly EntityHandle MemberForwarded;
			public readonly StringHandle ImportName;
			public readonly ModuleReferenceHandle ImportScope;

			public unsafe ImplMap(byte *ptr, int moduleRefSize, int memberForwardedTagRefSize, int stringHandleSize)
			{
				MappingFlags = (PInvokeAttributes)Helpers.GetValue(ptr, 2);
				MemberForwarded = Helpers.FromMemberForwardedTag((uint)Helpers.GetValue(ptr + 2, memberForwardedTagRefSize));
				ImportName = MetadataTokens.StringHandle(Helpers.GetValue(ptr + 2 + memberForwardedTagRefSize, stringHandleSize));
				ImportScope = MetadataTokens.ModuleReferenceHandle(Helpers.GetValue(ptr + 2 + memberForwardedTagRefSize + stringHandleSize, moduleRefSize));
			}
		}

		unsafe struct ImplMapEntry
		{
			readonly PEFile module;
			readonly MetadataReader metadata;
			readonly ImplMap implMap;

			public int RID { get; }

			public int Token => 0x1C000000 | RID;

			public int Offset { get; }

			public PInvokeAttributes MappingFlags => implMap.MappingFlags;

			public object MappingFlagsTooltip => new FlagsTooltip((int)implMap.MappingFlags, typeof(PInvokeAttributes));

			[StringFormat("X8")]
			public int MemberForwarded => MetadataTokens.GetToken(implMap.MemberForwarded);

			public string MemberForwardedTooltip {
				get {
					ITextOutput output = new PlainTextOutput();
					var context = new GenericContext(default(TypeDefinitionHandle), module);
					((EntityHandle)implMap.MemberForwarded).WriteTo(module, output, context);
					return output.ToString();
				}
			}

			[StringFormat("X8")]
			public object ImportScope => MetadataTokens.GetToken(implMap.ImportScope);

			public string ImportScopeTooltip {
				get {
					ITextOutput output = new PlainTextOutput();
					var context = new GenericContext(default(TypeDefinitionHandle), module);
					((EntityHandle)implMap.ImportScope).WriteTo(module, output, context);
					return output.ToString();
				}
			}

			public string ImportName => metadata.GetString(implMap.ImportName);

			public string ImportNameTooltip => $"{MetadataTokens.GetHeapOffset(implMap.ImportName):X} \"{ImportName}\"";

			public unsafe ImplMapEntry(PEFile module, byte* ptr, int metadataOffset, int row)
			{
				this.module = module;
				this.metadata = module.Metadata;
				this.RID = row;
				var rowOffset = metadata.GetTableMetadataOffset(TableIndex.ImplMap)
					+ metadata.GetTableRowSize(TableIndex.ImplMap) * (row - 1);
				this.Offset = metadataOffset + rowOffset;
				int moduleRefSize = metadata.GetTableRowCount(TableIndex.ModuleRef) < ushort.MaxValue ? 2 : 4;
				int memberForwardedTagRefSize = metadata.ComputeCodedTokenSize(32768, TableMask.MethodDef | TableMask.Field);
				int stringHandleSize = metadata.GetHeapSize(HeapIndex.String) < ushort.MaxValue ? 2 : 4;
				this.implMap = new ImplMap(ptr + rowOffset, moduleRefSize, memberForwardedTagRefSize, stringHandleSize);
			}
		}

		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.WriteCommentLine(output, "ImplMap");
		}
	}
}
