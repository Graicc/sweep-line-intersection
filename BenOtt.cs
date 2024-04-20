using Godot;
using System;
using System.Collections.Generic;

using Vector2 = Godot.Vector2;
using EQ = System.Collections.Generic.PriorityQueue<BenOtt.Event, float>;
using System.ComponentModel.Design;

/// <summary>
/// An implementation of Bentley–Ottmann algorithm for line segment intersection detection.
/// </summary>
/// Based on https://www.cs.cmu.edu/~15451-f17/lectures/lec21-sweepline.pdf
public partial class BenOtt : Godot.Node
{
	public class Segment
	{
		public Vector2 start;
		public Vector2 end;
		public Node node;

		public float Slope => (end.Y - start.Y) / (end.X - start.X);
		public float YIntercept => start.Y - Slope * start.X;
		public float ValueAt(float x) => Slope * x + YIntercept;
	}

	public class Event
	{
		public enum Type
		{
			Start,
			End,
			Intersection
		}

		public Type type;

		public float x;

		public Segment segment1;
		// Only for intersection events
		public Segment segment2;
	}

	bool CheckForIntersection(EQ events, Segment a, Segment b)
	{
		if (a == null || b == null)
		{
			return false;
		}

		// Find the intersection point
		if (b.Slope == a.Slope)
		{
			// Parallel lines
			GD.Print("Parallel lines");
			return false;
		}
		float x = (a.YIntercept - b.YIntercept) / (b.Slope - a.Slope);

		// If the intersection point is not within the range of the segments, return false
		float aLeft = Mathf.Min(a.start.X, a.end.X);
		float aRight = Mathf.Max(a.start.X, a.end.X);
		float bLeft = Mathf.Min(b.start.X, b.end.X);
		float bRight = Mathf.Max(b.start.X, b.end.X);
		if (x < aLeft || x > aRight || x < bLeft || x > bRight)
		{
			return false;
		}

		// insert the intersection event into the event queue
		Event intersectionEvent = new Event
		{
			type = Event.Type.Intersection,
			x = x,
			segment1 = a,
			segment2 = b,
		};

		events.Enqueue(intersectionEvent, x);

		return true;
	}

	public Godot.Collections.Array<Vector2> FindSegmentIntersections(Godot.Collections.Array<Node> nodes)
	{
		List<Segment> segments = new();

		foreach (var node in nodes)
		{
			Vector2 start = (Vector2)node.Get("start");
			Vector2 end = (Vector2)node.Get("end");

			if (start.X == end.X) {
				GD.Print("Skipping vertical line");
				continue;
			}

			// make sure that the start point is to the left of the end point
			// This makes everything easier later
			bool swapped = start.X > end.X;
			Segment segment = new Segment
			{
				start = swapped ? end : start,
				end = swapped ? start : end,
				node = node,
			};

			segments.Add(segment);
		}

		EQ events = new();

		Godot.Collections.Array<Vector2> results = new();

		// for each segment S, insert its start and end events into EQ
		foreach (Segment segment in segments)
		{
			Event startEvent = new Event
			{
				type = Event.Type.Start,
				x = segment.start.X,
				segment1 = segment,
				segment2 = segment,
			};
			events.Enqueue(startEvent, startEvent.x);

			Event endEvent = new Event
			{
				type = Event.Type.End,
				x = segment.end.X,
				segment1 = segment,
				segment2 = segment,
			};
			events.Enqueue(endEvent, endEvent.x);
		}

		// create an empty balanced tree SL
		List<Segment> SL = new();

		void insert(float x, Segment s)
		{
			// Find the correct position to insert the segment
			int i = 0;
			for (; i < SL.Count; i++)
			{
				if (SL[i].ValueAt(x) > s.ValueAt(x))
				{
					break;
				}
			}
			SL.Insert(i, s);
		}

		Segment prev(Segment s)
		{
			int i = SL.IndexOf(s);
			if (i <= 0)
			{
				return null;
			}
			return SL[i - 1];
		}

		Segment next(Segment s)
		{
			int i = SL.IndexOf(s);
			if (i >= SL.Count - 1)
			{
				return null;
			}
			return SL[i + 1];
		}

		// while (EQ is not empty)
		while (events.Count > 0)
		{
			if (events.Count > 10000) {
				// TODO: This should never be hit
				GD.Print("Too many events, breaking");
				return null;
			}
			var e = events.Dequeue();

			var s = e.segment1;
			if (e.type == Event.Type.Start)
			{
				insert(s.start.X, e.segment1);
				CheckForIntersection(events, s, next(s));
				CheckForIntersection(events, s, prev(s));
			}
			else if (e.type == Event.Type.End)
			{
				CheckForIntersection(events, prev(s), next(s));
				SL.Remove(e.segment1);
			}
			else if (e.type == Event.Type.Intersection)
			{
				results.Add(new Vector2(e.x, s.ValueAt(e.x)));

				// Swap the segments
				int i = SL.IndexOf(e.segment1);
				int j = SL.IndexOf(e.segment2);
				if (i == -1 || j == -1)
				{
					GD.Print("Segment not found in SL");
					continue;
				}
				SL[i] = e.segment2;
				SL[j] = e.segment1;

				if (i > j)
				{
					// 2 is after 1
					CheckForIntersection(events, e.segment1, prev(e.segment1)); // check before 1
					CheckForIntersection(events, e.segment2, next(e.segment2)); // check after 2
				}
				else
				{
					// 1 is after 2
					CheckForIntersection(events, e.segment1, next(e.segment1)); // check after 1
					CheckForIntersection(events, e.segment2, prev(e.segment2)); // check before 2
				}
			}
		}

		return results;
	}
}
