/* Shared prototype geometry. D3 owns tree packing; layouts never alter document order. */
(() => {
  'use strict';
  const items = [
    {key:'right',label:'单侧树形'}, {key:'balanced',label:'双侧平衡'},
    {key:'radial',label:'中心放射'}, {key:'down',label:'向下树形'},
    {key:'outline',label:'目录树'}, {key:'fishbone',label:'鱼骨图'}
  ];
  const valid = key => items.some(item => item.key === key);
  function layout(root, options = {}) {
    const mode = valid(options.mode) ? options.mode : 'right';
    const tree = d3.hierarchy(root,options.children || (node => node.children));
    const all = tree.descendants(), nodes = all.map(node => node.data), gap = 24;
    all.forEach(node => {
      options.measure?.(node.data);
      node.data.width ||= 224; node.data.height ||= 64;
      node.data.layoutSide = 'right';
    });
    const maxWidth = Math.max(...nodes.map(node => node.width)), step = maxWidth + 72;
    function place(node,cx,cy,side = 'right') { Object.assign(node.data,{cx,cy,layoutSide:side}); }
    function horizontal(part,side) {
      d3.tree().nodeSize([1,step]).separation((a,b) => (a.data.height+b.data.height)/2+gap)(part);
      const offset = part.x;
      part.each(node => place(node,side*node.depth*step,node.x-offset,side<0?'left':'right'));
    }
    function vertical(part) {
      d3.tree().nodeSize([1,1]).separation((a,b) => (a.data.width+b.data.width)/2+gap)(part);
      const heights = [], levels = [0], offset = part.x;
      part.each(node => heights[node.depth] = Math.max(heights[node.depth] || 0,node.data.height));
      for(let i=1;i<heights.length;i++) levels[i]=levels[i-1]+heights[i-1]/2+heights[i]/2+80;
      part.each(node => place(node,node.x-offset,levels[node.depth],'down'));
    }
    let spine = null;
    if(mode === 'balanced') {
      const sides = [[],[]], loads = [0,0];
      (tree.children || []).forEach(child => {
        const side = loads[0] <= loads[1] ? 0 : 1;
        sides[side].push(child.data); loads[side] += child.descendants().reduce((sum,node) => sum+node.data.height+gap,0);
      });
      sides.forEach((children,index) => {
        if(!children.length) return;
        const part = d3.hierarchy(root,node => node === root ? children : (options.children || (n => n.children))(node));
        horizontal(part,index === 0 ? 1 : -1);
      });
      place(tree,0,0);
    } else if(mode === 'radial') {
      d3.tree().size([Math.PI*2,Math.max(1,tree.height)*step]).separation((a,b) => (a.parent===b.parent?1:1.4)/Math.max(1,a.depth))(tree);
      all.forEach(node => place(node,Math.cos(node.x-Math.PI/2)*node.y,Math.sin(node.x-Math.PI/2)*node.y,'radial'));
      // Uniform expansion preserves angles while keeping variable-sized text boxes apart.
      let expansion = 1;
      for(let i=0;i<nodes.length;i++) for(let j=i+1;j<nodes.length;j++) {
        const a=nodes[i],b=nodes[j],dx=Math.abs(a.cx-b.cx),dy=Math.abs(a.cy-b.cy);
        const required=Math.min(dx>1e-8?((a.width+b.width)/2+gap)/dx:Infinity,dy>1e-8?((a.height+b.height)/2+gap)/dy:Infinity);
        if(Number.isFinite(required)) expansion=Math.max(expansion,required);
      }
      all.forEach(node => place(node,node.data.cx*expansion,node.data.cy*expansion,'radial'));
    } else if(mode === 'down') vertical(tree);
    else if(mode === 'outline') {
      let cursor=0;
      tree.eachBefore(node => {place(node,node.depth*64+node.data.width/2,cursor+node.data.height/2,'outline');cursor+=node.data.height+18;});
    } else if(mode === 'fishbone') {
      let cursor=0;
      (tree.children || []).forEach((branch,index) => {
        const part=d3.hierarchy(branch.data,options.children || (node => node.children));
        vertical(part);
        const group=part.descendants(),min=Math.min(...group.map(n=>n.data.cx-n.data.width/2));
        const max=Math.max(...group.map(n=>n.data.cx+n.data.width/2)),direction=index%2?1:-1;
        const offset=cursor-min,base=root.height/2+branch.data.height/2+100;
        group.forEach(node => place(node,node.data.cx+offset,direction*(base+node.data.cy),direction<0?'up':'down'));
        branch.data.fishAnchor={x:branch.data.cx+64,y:0};
        cursor+=max-min+100;
      });
      place(tree,cursor+root.width/2+40,0,'fishbone');
      if(tree.children?.length) spine={x:tree.children[0].data.fishAnchor.x,y:0};
    } else horizontal(tree,1);
    const minX=Math.min(...nodes.map(n=>n.cx-n.width/2)),minY=Math.min(...nodes.map(n=>n.cy-n.height/2));
    const dx=40-minX,dy=48-minY;
    nodes.forEach(node => {node.cx+=dx;node.cy+=dy;node.x=node.cx-node.width/2;node.y=node.cy-node.height/2;});
    const point=(x,y)=>`${x.toFixed(2)},${y.toFixed(2)}`;
    function ray(node,vx,vy) {
      const k=Math.min(vx?node.width/2/Math.abs(vx):Infinity,vy?node.height/2/Math.abs(vy):Infinity);
      return {x:node.cx+vx*k,y:node.cy+vy*k};
    }
    const links=all.filter(n=>n.parent).map(item => {
      const a=item.parent.data,b=item.data;
      let path;
      if(mode === 'fishbone' && item.parent === tree) {
        const anchor=b.fishAnchor;
        path=`M${point(anchor.x+dx,dy)} L${point(b.cx,b.cy+(b.cy<root.cy?b.height/2:-b.height/2))}`;
      } else if(mode === 'radial') {
        const vx=b.cx-a.cx,vy=b.cy-a.cy,start=ray(a,vx,vy),end=ray(b,-vx,-vy);
        path=`M${point(start.x,start.y)} L${point(end.x,end.y)}`;
      } else if(mode === 'down' || mode === 'fishbone') {
        const sign=b.cy<a.cy?-1:1,y1=a.cy+sign*a.height/2,y2=b.cy-sign*b.height/2,middle=(y1+y2)/2;
        path=`M${point(a.cx,y1)} C${point(a.cx,middle)} ${point(b.cx,middle)} ${point(b.cx,y2)}`;
      } else if(mode === 'outline') path=`M${point(a.x+16,a.y+a.height)} V${b.cy.toFixed(2)} H${b.x.toFixed(2)}`;
      else {
        const sign=b.cx<a.cx?-1:1,x1=a.cx+sign*a.width/2,x2=b.cx-sign*b.width/2,middle=(x1+x2)/2;
        path=`M${point(x1,a.cy)} C${point(middle,a.cy)} ${point(middle,b.cy)} ${point(x2,b.cy)}`;
      }
      return {parent:a,node:b,path};
    });
    if(spine) links.unshift({parent:root,node:root,path:`M${point(spine.x+dx,dy)} H${root.x.toFixed(2)}`});
    return {nodes,links,width:Math.max(...nodes.map(n=>n.x+n.width))+40,height:Math.max(...nodes.map(n=>n.y+n.height))+48};
  }
  window.VexMapLayout={items,valid,layout};
})();
