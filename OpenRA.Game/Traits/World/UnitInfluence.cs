#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenRA.FileFormats;

namespace OpenRA.Traits
{
	public class UnitInfluenceInfo : ITraitInfo
	{
		public object Create( ActorInitializer init ) { return new UnitInfluence( init.world ); }
	}

	public class UnitInfluence
	{
		class InfluenceNode
		{
			public InfluenceNode next;
			public SubCell subCell;
			public Actor actor;
		}
	
		InfluenceNode[,] influence;
		Map map;

		public UnitInfluence( World world )
		{
			map = world.Map;
			influence = new InfluenceNode[world.Map.MapSize.X, world.Map.MapSize.Y];

			world.ActorAdded += a => Add( a, a.TraitOrDefault<IOccupySpace>() );
			world.ActorRemoved += a => Remove( a, a.TraitOrDefault<IOccupySpace>() );
		}

		public IEnumerable<Actor> GetUnitsAt( int2 a )
		{
			if (!map.IsInMap(a)) yield break;

			for( var i = influence[ a.X, a.Y ] ; i != null ; i = i.next )
				if (!i.actor.Destroyed)
					yield return i.actor;
		}
		
		public bool HasFreeSubCell(int2 a)
		{
			if (!AnyUnitsAt(a))
				return true;
			
			return new[]{SubCell.TopLeft, SubCell.TopRight, SubCell.Center,
				SubCell.BottomLeft, SubCell.BottomRight}.Any(b => !AnyUnitsAt(a,b));
		}
		
		// This is only used inside Move, when we *know* that we can enter the cell.
		// If the cell contains something crushable, a naive check of 'is it empty' will fail.
		// Infantry can currently only crush crates/mines which take a whole cell, so we will ignore
		// the SubCell.FullCell case and hope that someone doesn't introduce subcell units
		// crushing other subcell units in the future.
		public SubCell GetFreeSubcell(int2 a, SubCell preferred)
		{
			if (preferred == SubCell.FullCell)
				return preferred;
			
			return new[]{ preferred, SubCell.TopLeft, SubCell.TopRight, SubCell.Center,
				SubCell.BottomLeft, SubCell.BottomRight}.First(b => 
			{
				var node = influence[ a.X, a.Y ];
				while (node != null)
				{
					if (node.subCell == b) return false;
					node = node.next;
				}
				return true;
			});
		}

		public bool AnyUnitsAt(int2 a)
		{
			return influence[ a.X, a.Y ] != null;
		}
		
		public bool AnyUnitsAt(int2 a, SubCell sub)
		{		
			var node = influence[ a.X, a.Y ];
			while (node != null)
			{
				if (node.subCell == sub || node.subCell == SubCell.FullCell) return true;
				node = node.next;
			}
			return false;
		}

		public void Add( Actor self, IOccupySpace unit )
		{
			if (unit != null)
				foreach( var c in unit.OccupiedCells() )
					influence[ c.First.X, c.First.Y ] = new InfluenceNode { next = influence[ c.First.X, c.First.Y ], subCell = c.Second, actor = self };
		}

		public void Remove( Actor self, IOccupySpace unit )
		{
			if (unit != null)
				foreach (var c in unit.OccupiedCells())
					RemoveInner( ref influence[ c.First.X, c.First.Y ], self );
		}

		void RemoveInner( ref InfluenceNode influenceNode, Actor toRemove )
		{
			if( influenceNode == null )
				return;
			else if( influenceNode.actor == toRemove )
				influenceNode = influenceNode.next;

			if (influenceNode != null)
				RemoveInner( ref influenceNode.next, toRemove );
		}

		public void Update(Actor self, IOccupySpace unit)
		{
			Remove(self, unit);
			if (!self.IsDead()) Add(self, unit);
		}
	}
}
