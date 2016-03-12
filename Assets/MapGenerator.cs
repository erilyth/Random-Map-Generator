using UnityEngine;
using System.Collections;
using System; //To use random generator
using System.Collections.Generic; //For Lists and Queues
using System.Linq;

public class MapGenerator : MonoBehaviour {

	public class Coord{
		public int xPos,yPos;
	};

	int[,] map;
	int[,] visited;

	public List<Vector3> vertices;
	public List<int> triangles;
	Mesh mesh;

	public int height;
	public int width;
	public int seed;
	public int fillPercent;
	public int smoothIterations;
	public int clearWallTreshold; //If there are isolated wall groups with number of cells less than this they are removed
	public int clearRoomTreshold;  //If there are isolated room groups with number of cells less than this they are removed

	struct Region{
		public int size;
		public int isMain;
		public int isConnectedToMain;
		public float distanceFromMain;
		public List<Coord> boundary;
		public List<Coord> contents;
	};

	List<Region> allRemainingRegions;
	List<Coord> lineStartingPoint; //To carve our that region
	List<Coord> lineEndingPoints; //To carve out that region

	// Use this for initialization
	void Start () {
		vertices = new List<Vector3> ();
		triangles = new List<int> ();
		visited = new int[height, width];
		CreateRandomMap ();
		RemoveSmallRooms (clearRoomTreshold); //To fill in rooms below size of treshold
		//In order to connect rooms, we select the largest room to be the main room, then we mark it as connected to main room, then we sort all other rooms in order of distance
		//from the main room, then traversing the rooms(least distance from main room comes first), we try and connect each room to the closest currently marked (connected to main room) room. Once that is done, we can mark
		//this room as connected to main room.
		ConnectRooms (); 
		RemoveSmallWalls (clearWallTreshold); //To clear walls below size of treshold
		CreateRender ();
	}

	/************************************************************************************************************************************************************************/

	void ConnectRooms(){
		//Use this function only after calling RemoveSmallRooms since that would give us the list of allRemainingRegions
		GetMainRegion ();
		SortRegions ();
		lineStartingPoint = new List<Coord> ();
		lineEndingPoints = new List<Coord> ();
		DrawLines ();
		CarvePaths ();
	}

	void GetMainRegion(){
		int maxSize = 0;
		Region largestMainRegion= new Region();
		Region temp;
		for(int i=0;i<allRemainingRegions.Count;i++){
			temp=allRemainingRegions[i];
			temp.isMain=0;
			temp.isConnectedToMain=0;
			allRemainingRegions[i]=temp;
			if(allRemainingRegions[i].size>maxSize){
				maxSize=allRemainingRegions[i].size;
			}
		}
		for(int i=0;i<allRemainingRegions.Count;i++){
			if(allRemainingRegions[i].size==maxSize){
				Region temp1=allRemainingRegions[i];
				temp1.isMain=1;
				temp1.isConnectedToMain=1;
				allRemainingRegions[i]=temp1;
				largestMainRegion=allRemainingRegions[i];
				break;
			}
		}
		for(int i=0;i<allRemainingRegions.Count;i++) {
			float bestDistance=1000000000f;
			foreach(Coord currentCoord in allRemainingRegions[i].boundary){
				foreach(Coord mainCoord in largestMainRegion.boundary){
					bestDistance=Math.Min(bestDistance,(currentCoord.xPos-mainCoord.xPos)*(currentCoord.xPos-mainCoord.xPos)+(currentCoord.yPos-mainCoord.yPos)*(currentCoord.yPos-mainCoord.yPos));
				}
			}
			Region temp2=allRemainingRegions[i];
			temp2.distanceFromMain=bestDistance;
			allRemainingRegions[i]=temp2;
		}
	}

	void SortRegions(){
		allRemainingRegions=allRemainingRegions.OrderBy (x => x.distanceFromMain).ToList (); //This sorts the list according to distanceFromMain
	}

	void DrawLines(){
		for(int i=0;i<allRemainingRegions.Count;i++) {
			if(allRemainingRegions[i].isConnectedToMain==1)
				continue;
			float bestDistance=1000000000f;
			Coord currentPoint= new Coord(), otherPoint=new Coord();
			foreach(Region other in allRemainingRegions) {
				if(other.isConnectedToMain==1){
					foreach(Coord currentCoord in allRemainingRegions[i].boundary){
						foreach(Coord otherCoord in other.boundary){
							if((currentCoord.xPos-otherCoord.xPos)*(currentCoord.xPos-otherCoord.xPos)+(currentCoord.yPos-otherCoord.yPos)*(currentCoord.yPos-otherCoord.yPos)<bestDistance)
							{
								bestDistance=(currentCoord.xPos-otherCoord.xPos)*(currentCoord.xPos-otherCoord.xPos)+(currentCoord.yPos-otherCoord.yPos)*(currentCoord.yPos-otherCoord.yPos);
								currentPoint=currentCoord;
								otherPoint=otherCoord;
							}
						}
					}
				}
			}
			Region temp=allRemainingRegions[i];
			temp.isConnectedToMain=1;
			allRemainingRegions[i]=temp;
			/*Vector3 start=new Vector3(-height/2+(float)currentPoint.xPos,0f,-width/2+(float)currentPoint.yPos-1.5f); //We draw lines so that we can walls from map to connect regions
			Vector3 end= new Vector3(-height/2+(float)otherPoint.xPos,0f,-width/2+(float)otherPoint.yPos-1.5f);
			Debug.DrawLine(start,end,Color.green,100f, false);*/
			lineStartingPoint.Add (currentPoint);
			lineEndingPoints.Add (otherPoint);
		}
	}

	void CarvePaths(){
		for(int i=0;i<lineStartingPoint.Count;i++){
			Coord start=lineStartingPoint[i];
			Coord end=lineEndingPoints[i];
			float slope=0f;
			float constant=0;
			int infSlope=0;
			if(end.xPos==start.xPos)
				infSlope=1;
			else
			{
				slope=(float)(end.yPos-start.yPos)/(float)(end.xPos-start.xPos);
				constant=(float)start.yPos-slope*start.xPos;
			}
			if(Math.Abs(end.xPos-start.xPos)>=Math.Abs (end.yPos-start.yPos)){
				if(start.xPos>end.xPos)
				{
					int tempx=start.xPos;
					int tempy=start.yPos;
					start.xPos=end.xPos;
					start.yPos=end.yPos;
					end.xPos=tempx;
					end.yPos=tempy;
				}
				for(int x=start.xPos;x<=end.xPos;x++){
					//print (slope*x+constant);
					if(slope*x+constant-(int)(slope*x+constant)>=0.5f)
						ClearAdjacentWalls(x,(int)(slope*x+constant)+1); //Slope wont be infinity
					else
						ClearAdjacentWalls(x,(int)(slope*x+constant)); //Slope wont be infinity
				}
			}
			else{ //Here we might encounter the infinite slope case
				if(start.yPos>end.yPos)
				{
					int tempx=start.xPos;
					int tempy=start.yPos;
					start.xPos=end.xPos;
					start.yPos=end.yPos;
					end.xPos=tempx;
					end.yPos=tempy;
				}
				for(int y=start.yPos;y<=end.yPos;y++){
					if(infSlope==1){
						ClearAdjacentWalls(start.xPos,y);
					}
					else{
						if((y-constant)/slope-(int)((y-constant)/slope)>=0.5)
							ClearAdjacentWalls((int)((y-constant)/slope)+1,y);
						else
							ClearAdjacentWalls((int)((y-constant)/slope),y);
					}
				}
			}
		}
	}

	void ClearAdjacentWalls(int x, int y) {
		for(int i=x-1; i<=x+1; i++){
			for(int j=y-1;j<=y+1;j++){
				if(i>0 && i<height-1 && j>0 && j<width-1){
					map[i,j]=0;
				}
			}
		}
	}

	/************************************************************************************************************************************************************************/

	void RemoveSmallRooms(int treshold){
		allRemainingRegions = new List<Region> ();
		List< List<Coord> > roomRegions = getAllRegions (0);
		foreach (List<Coord> currentRoomRegion in roomRegions) {
			if(currentRoomRegion.Count<treshold){
				foreach(Coord current in currentRoomRegion){
					map[current.xPos,current.yPos]=1; //We add walls in that region
				}
			}
			else{
				Region newRegion = new Region();
				newRegion.boundary= new List<Coord>();
				newRegion.contents=currentRoomRegion;
				newRegion.size=currentRoomRegion.Count;
				foreach(Coord current in currentRoomRegion){
					int tx=current.xPos; int ty=current.yPos;
					if((tx-1>=0 && map[tx-1,ty]==1) || (tx+1<height && map[tx+1,ty]==1) || (ty-1>=0 && map[tx,ty-1]==1) || (ty+1>=0 && map[tx,ty+1]==1))
						newRegion.boundary.Add (current);
				}
				allRemainingRegions.Add (newRegion);
			}
		}
	}

	void RemoveSmallWalls(int treshold){
		List< List<Coord> > roomRegions = getAllRegions (1);
		foreach (List<Coord> currentRoomRegion in roomRegions) {
			if(currentRoomRegion.Count<treshold){
				foreach(Coord current in currentRoomRegion){
					map[current.xPos,current.yPos]=0; //We add walls in that region
				}
			}
		}
	}

	//Flood Flow (Similar to a DFS) to get all cells of current Region
	List<Coord> getRegionCells(int x, int y){
		int typeOfCell = map [x, y];
		List<Coord> cellsInRegion = new List<Coord> (); //This will contain all the cells in current region
		Queue<Coord> queue = new Queue<Coord> ();
		Coord currentCoord = new Coord ();
		currentCoord.xPos = x;
		currentCoord.yPos = y;
		queue.Enqueue (currentCoord);
		visited [x, y] = 1;
		while (queue.Count!=0) {
			Coord current=queue.Dequeue();
			x=current.xPos; y=current.yPos;
			cellsInRegion.Add (current);
			if(x-1>=0 && visited[x-1,y]==0 && map[x-1,y]==typeOfCell){
				visited[x-1,y]=1;
				Coord temp = new Coord();
				temp.xPos=x-1;
				temp.yPos=y;
				queue.Enqueue(temp);
			}
			if(y-1>=0 && visited[x,y-1]==0 && map[x,y-1]==typeOfCell){
				visited[x,y-1]=1;
				Coord temp = new Coord();
				temp.xPos=x;
				temp.yPos=y-1;
				queue.Enqueue(temp);
			}
			if(x+1<height && visited[x+1,y]==0 && map[x+1,y]==typeOfCell){
				visited[x+1,y]=1;
				Coord temp = new Coord();
				temp.xPos=x+1;
				temp.yPos=y;
				queue.Enqueue(temp);
			}
			if(y+1<width && visited[x,y+1]==0 && map[x,y+1]==typeOfCell){
				visited[x,y+1]=1;
				Coord temp = new Coord();
				temp.xPos=x;
				temp.yPos=y+1;
				queue.Enqueue(temp);
			}
		}
		return cellsInRegion;
	}

	List< List<Coord> > getAllRegions(int typeRequired){
		List< List<Coord> > allRegions = new List< List<Coord> >();
		for(int x=0;x<height;x++){
			for(int y=0;y<width;y++){
				if(visited[x,y]==0 && map[x,y]==typeRequired){ //typeRequired is 1 for walls and 0 for rooms
					List<Coord> currentList = getRegionCells(x,y);
					allRegions.Add (currentList);
				}
			}
		}
		return allRegions;
	}

	/************************************************************************************************************************************************************************/

	void CreateRender(){
		mesh = new Mesh ();
		for (int x=0; x<height; x++) {
			for(int y=0;y<width;y++){
				if(map[x,y]==1){
					//Add all 8 vertices of a cube to our mesh
					Vector3 position1 = new Vector3 (-width / 2 + x-0.5f, 0.5f, -height / 2 + y-0.5f);
					Vector3 position2 = new Vector3 (-width / 2 + x+0.5f, 0.5f, -height / 2 + y-0.5f);
					Vector3 position3 = new Vector3 (-width / 2 + x+0.5f, 0.5f, -height / 2 + y+0.5f);
					Vector3 position4 = new Vector3 (-width / 2 + x-0.5f, 0.5f, -height / 2 + y+0.5f);
					Vector3 position5 = new Vector3 (-width / 2 + x-0.5f, -0.5f, -height / 2 + y-0.5f);
					Vector3 position6 = new Vector3 (-width / 2 + x+0.5f, -0.5f, -height / 2 + y-0.5f);
					Vector3 position7 = new Vector3 (-width / 2 + x+0.5f, -0.5f, -height / 2 + y+0.5f);
					Vector3 position8 = new Vector3 (-width / 2 + x-0.5f, -0.5f, -height / 2 + y+0.5f);
					int currentLength=vertices.Count-1;
					vertices.Add (position1);
					vertices.Add (position2);
					vertices.Add (position3);
					vertices.Add (position4);
					vertices.Add (position5);
					vertices.Add (position6);
					vertices.Add (position7);
					vertices.Add (position8);
					//If these vertices are clockwise, then the normals face one direction (ie. we can see from one direction)
					//If they are anti-clockwise, then the normals face other directions (ie. we can see from the other direction as well)
					//So we will render triangles in both clockwise and anti-clockwise directions
					//Clockwise Vertices

					int disableRight=0,disableLeft=0,disableFront=0,disableBack=0;

					if((x+1>=height || map[x+1,y]==0) && (y+1>=width || map[x,y+1]==0) && x-1>=0 && map[x-1,y]==1){ //RIGHT and FRONT with LEFT present
						triangles.Add (currentLength+8); triangles.Add (currentLength+6); triangles.Add (currentLength+2); 
						triangles.Add (currentLength+8); triangles.Add (currentLength+2); triangles.Add (currentLength+4);
						disableRight=1; disableFront=1;
						triangles.Add (currentLength+8); triangles.Add (currentLength+5); triangles.Add (currentLength+6); //TOP
						triangles.Add (currentLength+4); triangles.Add (currentLength+1); triangles.Add (currentLength+2); //BOTTOM
					}

					if((y+1>=width || map[x,y+1]==0) && (x-1<0 || map[x-1,y]==0) && y-1>=0 && map[x,y-1]==1){ //FRONT and LEFT with BACK present
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+7); 
						triangles.Add (currentLength+1); triangles.Add (currentLength+7); triangles.Add (currentLength+5);
						disableFront=1; disableLeft=1;
						triangles.Add (currentLength+1); triangles.Add (currentLength+2); triangles.Add (currentLength+3); //TOP
						triangles.Add (currentLength+5); triangles.Add (currentLength+6); triangles.Add (currentLength+7); //BOTTOM
					}

					if((x-1<0 || map[x-1,y]==0) && (y-1<0 || map[x,y-1]==0) && x+1<height && map[x+1,y]==1){ //LEFT and BACK with RIGHT present
						triangles.Add (currentLength+4); triangles.Add (currentLength+2); triangles.Add (currentLength+6); 
						triangles.Add (currentLength+4); triangles.Add (currentLength+6); triangles.Add (currentLength+8);
						disableLeft=1; disableBack=1;
						triangles.Add (currentLength+8); triangles.Add (currentLength+6); triangles.Add (currentLength+7); //TOP
						triangles.Add (currentLength+4); triangles.Add (currentLength+2); triangles.Add (currentLength+3); //BOTTOM
					}

					if((y-1<0 || map[x,y-1]==0) && (x+1>=height || map[x+1,y]==0) && y+1<width && map[x,y+1]==1){ //BACK and RIGHT with FRONT present
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+7); 
						triangles.Add (currentLength+1); triangles.Add (currentLength+7); triangles.Add (currentLength+5);
						disableBack=1; disableRight=1;
						triangles.Add (currentLength+5); triangles.Add (currentLength+7); triangles.Add (currentLength+8); //TOP
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+4); //BOTTOM
					}

					if(disableBack==0 && disableLeft==0 && disableRight==0 && disableFront==0){
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+2); //BOTTOM
						triangles.Add (currentLength+1); triangles.Add (currentLength+4); triangles.Add (currentLength+3); //BOTTOM
						triangles.Add (currentLength+5); triangles.Add (currentLength+7); triangles.Add (currentLength+6); //TOP
						triangles.Add (currentLength+5); triangles.Add (currentLength+8); triangles.Add (currentLength+7); //TOP
					}

					if(x+1>=height || map[x+1,y]==0 && disableRight==0){ //If there is an adjacent filled cube, then we dont draw triangles on this side since they are not needed
						triangles.Add (currentLength+6); triangles.Add (currentLength+3); triangles.Add (currentLength+2); //RIGHT
						triangles.Add (currentLength+6); triangles.Add (currentLength+7); triangles.Add (currentLength+3); //RIGHT
					}
					if(x-1<0 || map[x-1,y]==0 && disableLeft==0){
						triangles.Add (currentLength+5); triangles.Add (currentLength+4); triangles.Add (currentLength+1); //LEFT
						triangles.Add (currentLength+5); triangles.Add (currentLength+8); triangles.Add (currentLength+4); //LEFT
					}
					if(y+1>=width || map[x,y+1]==0 && disableFront==0){
						triangles.Add (currentLength+8); triangles.Add (currentLength+3); triangles.Add (currentLength+4); //FRONT
						triangles.Add (currentLength+8); triangles.Add (currentLength+7); triangles.Add (currentLength+3); //FRONT
					}
					if(y-1<0 || map[x,y-1]==0 && disableBack==0){ 
						triangles.Add (currentLength+5); triangles.Add (currentLength+2); triangles.Add (currentLength+1); //BACK
						triangles.Add (currentLength+5); triangles.Add (currentLength+6); triangles.Add (currentLength+2); //BACK
					}

					//Anti-clockwise Vertices

					if((x+1>=height || map[x+1,y]==0) && (y+1>=width || map[x,y+1]==0) && x-1>=0 && map[x-1,y]==1){ //RIGHT and FRONT with LEFT present
						triangles.Add (currentLength+8); triangles.Add (currentLength+2); triangles.Add (currentLength+6); 
						triangles.Add (currentLength+8); triangles.Add (currentLength+4); triangles.Add (currentLength+2);
						disableRight=1; disableFront=1;
						triangles.Add (currentLength+8); triangles.Add (currentLength+6); triangles.Add (currentLength+5); //TOP
						triangles.Add (currentLength+4); triangles.Add (currentLength+2); triangles.Add (currentLength+1); //BOTTOM
					}
					
					if((y+1>=width || map[x,y+1]==0) && (x-1<0 || map[x-1,y]==0) && y-1>=0 && map[x,y-1]==1){ //FRONT and LEFT with BACK present
						triangles.Add (currentLength+1); triangles.Add (currentLength+7); triangles.Add (currentLength+3); 
						triangles.Add (currentLength+1); triangles.Add (currentLength+5); triangles.Add (currentLength+7);
						disableFront=1; disableLeft=1;
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+2); //TOP
						triangles.Add (currentLength+5); triangles.Add (currentLength+7); triangles.Add (currentLength+6); //BOTTOM
					}
					
					if((x-1<0 || map[x-1,y]==0) && (y-1<0 || map[x,y-1]==0) && x+1<height && map[x+1,y]==1){ //LEFT and BACK with RIGHT present
						triangles.Add (currentLength+4); triangles.Add (currentLength+6); triangles.Add (currentLength+2); 
						triangles.Add (currentLength+4); triangles.Add (currentLength+8); triangles.Add (currentLength+6);
						disableLeft=1; disableBack=1;
						triangles.Add (currentLength+8); triangles.Add (currentLength+7); triangles.Add (currentLength+6); //TOP
						triangles.Add (currentLength+4); triangles.Add (currentLength+3); triangles.Add (currentLength+2); //BOTTOM
					}
					
					if((y-1<0 || map[x,y-1]==0) && (x+1>=height || map[x+1,y]==0) && y+1<width && map[x,y+1]==1){ //BACK and RIGHT with FRONT present
						triangles.Add (currentLength+1); triangles.Add (currentLength+7); triangles.Add (currentLength+3); 
						triangles.Add (currentLength+1); triangles.Add (currentLength+5); triangles.Add (currentLength+7);
						disableBack=1; disableRight=1;
						triangles.Add (currentLength+5); triangles.Add (currentLength+8); triangles.Add (currentLength+7); //TOP
						triangles.Add (currentLength+1); triangles.Add (currentLength+4); triangles.Add (currentLength+3); //BOTTOM
					}

					if(disableBack==0 && disableLeft==0 && disableRight==0 && disableFront==0){
						triangles.Add (currentLength+1); triangles.Add (currentLength+2); triangles.Add (currentLength+3); //BOTTOM
						triangles.Add (currentLength+1); triangles.Add (currentLength+3); triangles.Add (currentLength+4); //BOTTOM
						triangles.Add (currentLength+5); triangles.Add (currentLength+6); triangles.Add (currentLength+7); //TOP
						triangles.Add (currentLength+5); triangles.Add (currentLength+7); triangles.Add (currentLength+8); //TOP
					}

					if(x+1>=height || map[x+1,y]==0 && disableRight==0){ //If there is an adjacent filled cube, then we dont draw triangles on this side since they are not needed
						triangles.Add (currentLength+6); triangles.Add (currentLength+2); triangles.Add (currentLength+3); //RIGHT
						triangles.Add (currentLength+6); triangles.Add (currentLength+3); triangles.Add (currentLength+7); //RIGHT
					}
					if(x-1<0 || map[x-1,y]==0 && disableLeft==0){
						triangles.Add (currentLength+5); triangles.Add (currentLength+1); triangles.Add (currentLength+4); //LEFT
						triangles.Add (currentLength+5); triangles.Add (currentLength+4); triangles.Add (currentLength+8); //LEFT
					}
					if(y+1>=width || map[x,y+1]==0 && disableFront==0){
						triangles.Add (currentLength+8); triangles.Add (currentLength+4); triangles.Add (currentLength+3); //FRONT
						triangles.Add (currentLength+8); triangles.Add (currentLength+3); triangles.Add (currentLength+7); //FRONT
					}
					if(y-1<0 || map[x,y-1]==0 && disableBack==0){ 
						triangles.Add (currentLength+5); triangles.Add (currentLength+1); triangles.Add (currentLength+2); //BACK
						triangles.Add (currentLength+5); triangles.Add (currentLength+2); triangles.Add (currentLength+6); //BACK
					}
				}
			}
		}
		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		GetComponent<MeshFilter>().mesh = mesh;
		gameObject.AddComponent<MeshCollider> (); //We add a mesh collider to our created mesh
	}

	/************************************************************************************************************************************************************************/

	void CreateRandomMap(){ //Randomly add walls and make map ready for cellular automata
		map = new int[height, width];
		System.Random generator = new System.Random (seed);
		for (int x=0; x<height; x++) {
			for(int y=0;y<width;y++){
				if(x==0 || y==0 || x==height-1 || y==width-1)
					map[x,y]=1; //Boundaries of map
				else
					map[x,y]=(generator.Next(0,100)<fillPercent)?1:0; 
			}
		}
		for (int i=0; i<smoothIterations; i++) {
			SmoothMap();
		}
	}

	/************************************************************************************************************************************************************************/

	//Cellular Automata
	void SmoothMap(){
		for (int x=0; x<height; x++) {
			for(int y=0;y<width;y++){
				int count=AdjacentWalls(x,y);
				if(count>4)
					map[x,y]=1;
				if(count<4)
					map[x,y]=0;
			}
		}
	}

	int AdjacentWalls(int x, int y){
		int adj = 0;
		for(int i=x-1;i<=x+1;i++){
			for(int j=y-1;j<=y+1;j++){
				if(i>=0 && i<height && j>=0 && j<width){
					if(i!=x || j!=y){
						adj+=map[i,j];
					}
				}
				else
					adj++; //We consider out of range as a wall so that our boundaries are not erased (Important)
			}
		}
		return adj;
	}

	/************************************************************************************************************************************************************************/

	/*void OnDrawGizmos(){
		if (map != null) {
			for (int x=0; x<height; x++) {
				for (int y=0; y<width; y++) {
					Gizmos.color = (map [x, y] == 1) ? Color.black : Color.white;
					Vector3 position = new Vector3 (-width / 2 + x, 0, -height / 2 + y);
					Gizmos.DrawCube (position, Vector3.one); //Draw a helping box gizmo with size 1 on all axes
				}
			}
		}
	}*/
}
