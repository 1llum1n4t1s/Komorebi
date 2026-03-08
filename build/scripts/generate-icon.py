#!/usr/bin/env python3
"""
Generate Komorebi app icon in all required formats.
Design: Modern 木漏れ日 (komorebi / sunlight filtering through leaves)

Usage:
  Step 1: python generate-icon.py svg     ↁECreates Canvas-based HTML preview
  Step 2: (open HTML in browser ↁEauto-downloads 1024x1024 PNG)
  Step 3: python generate-icon.py pack <png_path>  ↁECreates .ico, .icns, project PNGs
"""

import io
import struct
import sys
from pathlib import Path
from PIL import Image

PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent


def create_canvas_html():
    """
    v7: Photo-realistic Canvas renderer -- trypophobia-safe.

    Key improvements over v6:
      - Per-pixel noise texture via getImageData/putImageData (organic feel)
      - Film grain for photographic quality
      - Color temperature variation (warm highlights, cool shadows)
      - Per-pixel vignette (smooth edge darkening)
      - 3D tree trunks with highlight/shadow sides
      - Backlit leaves with translucency glow
      - Smoother opening path (32 segments + 4-octave noise)
      - Richer sky gradient with more color stops
    Still trypophobia-safe: NO clusters of small shapes.
    """
    return r'''<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>Komorebi Icon v7</title>
<style>
body{margin:0;padding:40px;background:#111;display:flex;flex-direction:column;align-items:center;font-family:system-ui;color:#ccc}
h1{color:#a5d6a7;margin-bottom:8px}
canvas{border-radius:8px}
#st{color:#81c784;font-size:14px;margin:10px}
</style>
</head>
<body>
<h1>Komorebi Icon v7</h1>
<p>Photo-realistic + trypophobia-safe</p>
<canvas id="c" width="1024" height="1024"></canvas>
<div id="st">Rendering...</div>
<script>
'use strict';

/* ===== NOISE ===== */
function makeNoise(seed){
  const N=256,p=new Uint8Array(512),g=new Float32Array(N);
  let s=seed|0;
  for(let i=0;i<N;i++){
    s=(s*1103515245+12345)&0x7fffffff;
    g[i]=s/0x7fffffff*2-1; p[i]=i;
  }
  for(let i=N-1;i>0;i--){
    s=(s*1103515245+12345)&0x7fffffff;
    const j=(s>>>0)%(i+1);[p[i],p[j]]=[p[j],p[i]];
  }
  for(let i=0;i<N;i++)p[i+N]=p[i];
  return(x,y)=>{
    const xi=Math.floor(x)&(N-1),yi=Math.floor(y)&(N-1);
    const xf=x-Math.floor(x),yf=y-Math.floor(y);
    const u=xf*xf*(3-2*xf),v=yf*yf*(3-2*yf);
    const a=g[p[p[xi]+yi]],b=g[p[p[xi+1]+yi]];
    const c=g[p[p[xi]+yi+1]],d=g[p[p[xi+1]+yi+1]];
    return(a+u*(b-a))+(v*((c+u*(d-c))-(a+u*(b-a))));
  };
}
function fbm(fn,x,y,oct){
  let v=0,a=1,f=1,m=0;
  for(let i=0;i<(oct||4);i++){v+=a*fn(x*f,y*f);m+=a;a*=.5;f*=2;}
  return v/m;
}
function clamp(v){return Math.max(0,Math.min(255,v|0));}

/* ===== HELPERS ===== */
function rrect(c,x,y,w,h,r){
  c.beginPath();c.moveTo(x+r,y);c.lineTo(x+w-r,y);
  c.arcTo(x+w,y,x+w,y+r,r);c.lineTo(x+w,y+h-r);
  c.arcTo(x+w,y+h,x+w-r,y+h,r);c.lineTo(x+r,y+h);
  c.arcTo(x,y+h,x,y+h-r,r);c.lineTo(x,y+r);
  c.arcTo(x,y,x+r,y,r);c.closePath();
}
function addRRect(c,x,y,w,h,r){
  c.moveTo(x+r,y);c.lineTo(x+w-r,y);
  c.arcTo(x+w,y,x+w,y+r,r);c.lineTo(x+w,y+h-r);
  c.arcTo(x+w,y+h,x+w-r,y+h,r);c.lineTo(x+r,y+h);
  c.arcTo(x,y+h,x,y+h-r,r);c.lineTo(x,y+r);
  c.arcTo(x,y,x+r,y,r);c.closePath();
}

/* Opening path with 32 segments + 4-octave noise for smooth organic edge */
function addOpeningPath(ctx,cx,cy,rx,ry,nfn,ns){
  const N=32,pts=[];
  for(let i=0;i<N;i++){
    const a=(i/N)*Math.PI*2-Math.PI/2;
    const nv=fbm(nfn,Math.cos(a)*ns+5,Math.sin(a)*ns+5,4);
    pts.push([cx+Math.cos(a)*rx*(1+nv*.15), cy+Math.sin(a)*ry*(1+nv*.15)]);
  }
  ctx.moveTo(pts[0][0],pts[0][1]);
  for(let i=0;i<N;i++){
    const curr=pts[i],next=pts[(i+1)%N];
    ctx.quadraticCurveTo(curr[0],curr[1],(curr[0]+next[0])/2,(curr[1]+next[1])/2);
  }
  ctx.closePath();
}

/* ===== BACKLIT LEAF ===== */
function drawLeaf(ctx,x,y,len,wid,ang,backlit){
  ctx.save();ctx.translate(x,y);ctx.rotate(ang);
  ctx.beginPath();ctx.moveTo(0,0);
  ctx.bezierCurveTo(len*.25,-wid*.9, len*.55,-wid*.85, len*.95,-wid*.1);
  ctx.bezierCurveTo(len*.95,wid*.1, len*.55,wid*.85, len*.25,wid*.9);
  ctx.closePath();
  const lg=ctx.createLinearGradient(0,0,len,0);
  if(backlit){
    lg.addColorStop(0,'#2E5A1A');lg.addColorStop(.2,'#4A8828');
    lg.addColorStop(.5,'#6AAA38');lg.addColorStop(.8,'#5A9830');lg.addColorStop(1,'#3A7020');
  } else {
    lg.addColorStop(0,'#1A3A0E');lg.addColorStop(.3,'#2A5A18');
    lg.addColorStop(.6,'#1E4812');lg.addColorStop(1,'#14340E');
  }
  ctx.fillStyle=lg;ctx.fill();
  if(backlit){
    ctx.globalCompositeOperation='screen';
    ctx.strokeStyle='rgba(200,240,100,0.3)';ctx.lineWidth=2.5;ctx.stroke();
    ctx.globalCompositeOperation='source-over';
  }
  // Central vein
  ctx.beginPath();ctx.moveTo(len*.05,0);
  ctx.bezierCurveTo(len*.3,wid*.05,len*.6,-wid*.03,len*.9,0);
  ctx.strokeStyle=backlit?'rgba(120,180,60,.4)':'rgba(20,60,10,.35)';
  ctx.lineWidth=1.8;ctx.stroke();
  // Side veins
  for(let i=1;i<=4;i++){
    const t=i*.19+.05,px=len*t;
    const sp=wid*.7*(1-Math.pow(Math.abs(t-.45)*2.2,1.5));
    if(sp>2){
      ctx.beginPath();ctx.moveTo(px,0);ctx.quadraticCurveTo(px+len*.06,-sp*.6,px+len*.1,-sp);
      ctx.moveTo(px,0);ctx.quadraticCurveTo(px+len*.06,sp*.6,px+len*.1,sp);
      ctx.strokeStyle=backlit?'rgba(100,160,50,.25)':'rgba(20,50,10,.2)';
      ctx.lineWidth=.8;ctx.stroke();
    }
  }
  ctx.restore();
}

/* ===== TRUNK with 3D highlight ===== */
function drawTrunk(ctx,x0,y0,cx1,cy1,cx2,cy2,x3,y3,w,hlDir){
  const bg=ctx.createLinearGradient(x0-w*hlDir,y0,x0+w*hlDir,y0);
  bg.addColorStop(0,'#3E2723');bg.addColorStop(.4,'#4E342E');
  bg.addColorStop(.7,'#3E2723');bg.addColorStop(1,'#1A0E08');
  ctx.lineWidth=w;ctx.strokeStyle=bg;ctx.lineCap='round';
  ctx.beginPath();ctx.moveTo(x0,y0);ctx.bezierCurveTo(cx1,cy1,cx2,cy2,x3,y3);ctx.stroke();
  // Highlight streak
  ctx.save();ctx.globalAlpha=.3;ctx.lineWidth=w*.12;ctx.strokeStyle='#8D6E63';
  const off=w*.25*hlDir;
  ctx.beginPath();ctx.moveTo(x0+off,y0);
  ctx.bezierCurveTo(cx1+off,cy1,cx2+off*.7,cy2,x3+off*.5,y3);ctx.stroke();ctx.restore();
}

/* ========================================= */
/*              MAIN RENDER                  */
/* ========================================= */
function render(){
  const S=1024,PAD=32,SZ=S-PAD*2,CR=192;
  const CX=S/2,CY=S*.4;
  const cv=document.getElementById('c');
  const ctx=cv.getContext('2d');
  const n1=makeNoise(42),n2=makeNoise(137),n3=makeNoise(256);

  // ---- CLIP to rounded rect ----
  ctx.save();
  rrect(ctx,PAD,PAD,SZ,SZ,CR);
  ctx.clip();

  // ============================
  // 1. SKY GRADIENT (warm, rich)
  // ============================
  const sky=ctx.createRadialGradient(CX,CY,0,CX,CY,S*.58);
  sky.addColorStop(0,'#FFFEF8');sky.addColorStop(.08,'#FFF8E1');
  sky.addColorStop(.18,'#FFF0C0');sky.addColorStop(.30,'#E8D890');
  sky.addColorStop(.45,'#B0C878');sky.addColorStop(.60,'#6AAA48');
  sky.addColorStop(.75,'#388E3C');sky.addColorStop(.88,'#1B5E20');
  sky.addColorStop(1,'#0D3B0F');
  ctx.fillStyle=sky;ctx.fillRect(0,0,S,S);

  // ============================
  // 2. SUN BLOOM (layered soft glow)
  // ============================
  for(const[r,c]of[[.14,'rgba(255,255,250,.9)'],[.22,'rgba(255,252,220,.45)'],
    [.32,'rgba(255,245,180,.18)'],[.42,'rgba(220,240,180,.06)']]){
    const g=ctx.createRadialGradient(CX,CY,0,CX,CY,S*r);
    g.addColorStop(0,c);g.addColorStop(1,'rgba(0,0,0,0)');
    ctx.fillStyle=g;ctx.fillRect(0,0,S,S);
  }

  // ============================
  // 3. GOD RAYS (soft, wide)
  // ============================
  ctx.save();ctx.globalCompositeOperation='screen';
  for(const r of[{ox:0,w:70,sp:200,a:.18},{ox:-100,w:50,sp:180,a:.10},
    {ox:110,w:55,sp:185,a:.12},{ox:-220,w:35,sp:160,a:.05},{ox:240,w:32,sp:155,a:.04}]){
    const rx=CX+r.ox;
    const rg=ctx.createLinearGradient(rx,CY-80,rx,S);
    rg.addColorStop(0,'rgba(255,255,230,'+r.a+')');
    rg.addColorStop(.15,'rgba(255,248,200,'+(r.a*.6)+')');
    rg.addColorStop(.5,'rgba(220,240,180,'+(r.a*.1)+')');
    rg.addColorStop(1,'rgba(180,220,160,0)');
    ctx.beginPath();ctx.moveTo(rx-r.w/2,CY-80);ctx.lineTo(rx+r.w/2,CY-80);
    ctx.lineTo(rx+r.sp/2,S);ctx.lineTo(rx-r.sp/2,S);ctx.closePath();
    ctx.fillStyle=rg;ctx.fill();
  }
  ctx.restore();

  // ============================
  // 4. CANOPY FRAME (evenodd)
  // ============================
  ctx.save();
  ctx.shadowColor='rgba(0,15,0,.5)';ctx.shadowBlur=30;ctx.shadowOffsetY=8;
  ctx.beginPath();
  addRRect(ctx,PAD,PAD,SZ,SZ,CR);
  addOpeningPath(ctx,CX,CY,180,168,n1,2.5);
  const cg=ctx.createRadialGradient(CX,CY,S*.12,CX,CY,S*.6);
  cg.addColorStop(0,'#4CAF50');cg.addColorStop(.1,'#43A047');
  cg.addColorStop(.25,'#388E3C');cg.addColorStop(.45,'#2E7D32');
  cg.addColorStop(.65,'#1B5E20');cg.addColorStop(.85,'#0D3B0F');
  cg.addColorStop(1,'#071F08');
  ctx.fillStyle=cg;ctx.fill('evenodd');
  ctx.shadowBlur=0;ctx.restore();

  // ============================
  // 5. CANOPY INNER EDGE GLOW
  // ============================
  ctx.save();ctx.beginPath();
  addOpeningPath(ctx,CX,CY,210,195,n1,2.5);
  const eg=ctx.createRadialGradient(CX,CY,S*.1,CX,CY,S*.23);
  eg.addColorStop(0,'rgba(76,175,80,0)');eg.addColorStop(.3,'rgba(56,142,60,.06)');
  eg.addColorStop(.6,'rgba(46,125,50,.15)');eg.addColorStop(.85,'rgba(27,94,32,.3)');
  eg.addColorStop(1,'rgba(13,59,15,.4)');
  ctx.fillStyle=eg;ctx.fill();ctx.restore();

  // ============================
  // 6. CANOPY DEPTH LAYERS
  // ============================
  // Upper-left: warmer
  ctx.save();ctx.globalAlpha=.3;ctx.beginPath();
  ctx.moveTo(PAD,PAD);ctx.lineTo(CX-30,PAD);
  ctx.bezierCurveTo(CX-70,PAD+60,CX-140,CY-90,CX-110,CY);
  ctx.bezierCurveTo(CX-140,CY+50,PAD+80,CY-10,PAD,CY-50);ctx.closePath();
  const dl1=ctx.createRadialGradient(PAD+160,CY-80,20,PAD+160,CY-80,280);
  dl1.addColorStop(0,'#558B2F');dl1.addColorStop(1,'#2E7D32');
  ctx.fillStyle=dl1;ctx.fill();ctx.restore();
  // Upper-right: cooler
  ctx.save();ctx.globalAlpha=.25;ctx.beginPath();
  ctx.moveTo(CX+30,PAD);ctx.lineTo(S-PAD,PAD);ctx.lineTo(S-PAD,CY-50);
  ctx.bezierCurveTo(S-PAD-80,CY-10,CX+140,CY+50,CX+110,CY);
  ctx.bezierCurveTo(CX+140,CY-90,CX+70,PAD+60,CX+30,PAD);ctx.closePath();
  const dl2=ctx.createRadialGradient(S-PAD-160,CY-80,20,S-PAD-160,CY-80,280);
  dl2.addColorStop(0,'#33691E');dl2.addColorStop(1,'#1B5E20');
  ctx.fillStyle=dl2;ctx.fill();ctx.restore();
  // Bottom: deep
  ctx.save();ctx.globalAlpha=.3;ctx.beginPath();
  ctx.moveTo(PAD,CY+180);ctx.bezierCurveTo(PAD+100,CY+140,CX-80,CY+100,CX,CY+85);
  ctx.bezierCurveTo(CX+80,CY+100,S-PAD-100,CY+140,S-PAD,CY+180);
  ctx.lineTo(S-PAD,S-PAD);ctx.lineTo(PAD,S-PAD);ctx.closePath();
  const dl3=ctx.createLinearGradient(CX,CY+100,CX,S-PAD);
  dl3.addColorStop(0,'#1A3A0E');dl3.addColorStop(1,'#0A1F06');
  ctx.fillStyle=dl3;ctx.fill();ctx.restore();
  // Edges
  ctx.save();ctx.globalAlpha=.15;ctx.beginPath();
  ctx.moveTo(PAD,CY+20);ctx.bezierCurveTo(PAD+50,CY+80,PAD+40,CY+180,PAD+30,CY+280);
  ctx.lineTo(PAD,S-PAD);ctx.lineTo(PAD,CY+20);ctx.closePath();
  ctx.fillStyle='#558B2F';ctx.fill();ctx.restore();
  ctx.save();ctx.globalAlpha=.12;ctx.beginPath();
  ctx.moveTo(S-PAD,CY+30);ctx.bezierCurveTo(S-PAD-50,CY+90,S-PAD-40,CY+190,S-PAD-30,CY+290);
  ctx.lineTo(S-PAD,S-PAD);ctx.lineTo(S-PAD,CY+30);ctx.closePath();
  ctx.fillStyle='#1B5E20';ctx.fill();ctx.restore();

  // ============================
  // 7. TREE TRUNKS (3D)
  // ============================
  ctx.save();
  ctx.shadowColor='rgba(0,10,0,.4)';ctx.shadowBlur=18;ctx.shadowOffsetY=5;
  drawTrunk(ctx, 85,S-PAD, 92,S*.55, 115,CY+80, 175,CY-50, 48,-1);
  drawTrunk(ctx, S-85,S-PAD, S-92,S*.55, S-115,CY+80, S-175,CY-50, 42,1);
  drawTrunk(ctx, 320,S-PAD, 340,S*.65, 360,CY+80, 330,CY-30, 22,-1);
  ctx.shadowBlur=0;
  // Branches
  const bk='#3E2723';ctx.strokeStyle=bk;ctx.lineCap='round';
  ctx.lineWidth=18;
  ctx.beginPath();ctx.moveTo(175,CY-50);ctx.bezierCurveTo(220,CY-120,300,CY-160,CX-20,CY-165);ctx.stroke();
  ctx.beginPath();ctx.moveTo(S-175,CY-50);ctx.bezierCurveTo(S-220,CY-120,S-300,CY-160,CX+20,CY-165);ctx.stroke();
  ctx.lineWidth=10;
  ctx.beginPath();ctx.moveTo(130,CY+30);ctx.bezierCurveTo(95,CY+10,60,CY-5,PAD,CY-15);ctx.stroke();
  ctx.beginPath();ctx.moveTo(S-130,CY+30);ctx.bezierCurveTo(S-95,CY+10,S-60,CY-5,S-PAD,CY-15);ctx.stroke();
  ctx.beginPath();ctx.moveTo(175,CY-50);ctx.bezierCurveTo(140,CY-100,100,CY-130,PAD+10,CY-155);ctx.stroke();
  ctx.beginPath();ctx.moveTo(S-175,CY-50);ctx.bezierCurveTo(S-140,CY-100,S-100,CY-130,S-PAD-10,CY-155);ctx.stroke();
  ctx.lineWidth=7;
  ctx.beginPath();ctx.moveTo(150,CY-20);ctx.bezierCurveTo(115,CY-40,80,CY-55,PAD+15,CY-65);ctx.stroke();
  ctx.beginPath();ctx.moveTo(S-150,CY-20);ctx.bezierCurveTo(S-115,CY-40,S-80,CY-55,S-PAD-15,CY-65);ctx.stroke();
  // Top fork
  ctx.lineWidth=9;
  ctx.beginPath();ctx.moveTo(CX-20,CY-165);ctx.bezierCurveTo(CX-30,CY-200,CX-40,CY-240,CX-55,CY-270);ctx.stroke();
  ctx.beginPath();ctx.moveTo(CX+20,CY-165);ctx.bezierCurveTo(CX+30,CY-200,CX+40,CY-240,CX+55,CY-270);ctx.stroke();
  ctx.lineWidth=5;
  ctx.beginPath();ctx.moveTo(CX-55,CY-270);ctx.bezierCurveTo(CX-100,CY-285,CX-180,CY-290,PAD,CY-295);ctx.stroke();
  ctx.beginPath();ctx.moveTo(CX+55,CY-270);ctx.bezierCurveTo(CX+100,CY-285,CX+180,CY-290,S-PAD,CY-295);ctx.stroke();
  ctx.restore();

  // ============================
  // 8. LEAVES (backlit, large)
  // ============================
  for(const[lx,ly,ll,lw,la,lb]of[
    [CX-170,CY-35,75,22,.35,true],[CX+175,CY-25,70,20,-.4,true],
    [CX-115,CY-135,60,18,.85,true],[CX+120,CY-130,55,16,-.75,true],
    [CX-130,CY+115,58,17,.5,false],[CX+140,CY+125,62,19,-.55,false],
    [CX-30,CY-165,48,14,1.3,true],[CX+35,CY-158,50,15,-1.1,true],
  ]) drawLeaf(ctx,lx,ly,ll,lw,la,lb);

  // ============================
  // 9. BOKEH (4, well-separated)
  // ============================
  for(const[bx,by,br,ba]of[[CX-55,CY+65,28,.4],[CX+75,CY+115,22,.3],
    [CX+5,CY+175,24,.22],[CX-100,CY-10,18,.28]]){
    let bg=ctx.createRadialGradient(bx,by,0,bx,by,br*2.5);
    bg.addColorStop(0,'rgba(255,255,240,'+ba*.3+')');bg.addColorStop(.4,'rgba(255,255,220,'+ba*.1+')');
    bg.addColorStop(1,'rgba(255,255,200,0)');
    ctx.fillStyle=bg;ctx.beginPath();ctx.arc(bx,by,br*2.5,0,Math.PI*2);ctx.fill();
    bg=ctx.createRadialGradient(bx,by,0,bx,by,br);
    bg.addColorStop(0,'rgba(255,255,255,'+ba*.8+')');bg.addColorStop(.5,'rgba(255,252,230,'+ba*.4+')');
    bg.addColorStop(1,'rgba(255,248,200,0)');
    ctx.fillStyle=bg;ctx.beginPath();ctx.arc(bx,by,br,0,Math.PI*2);ctx.fill();
  }

  // ============================
  // 10. SPARKLES (5)
  // ============================
  for(const[sx,sy,sr,sa]of[[CX-15,CY+5,3.5,.85],[CX+32,CY+60,2.8,.7],
    [CX-52,CY+95,2.5,.6],[CX+68,CY+42,2.2,.55],[CX+12,CY-28,2,.62]]){
    let sg=ctx.createRadialGradient(sx,sy,0,sx,sy,sr*4);
    sg.addColorStop(0,'rgba(255,255,255,'+sa*.4+')');sg.addColorStop(1,'rgba(255,255,230,0)');
    ctx.fillStyle=sg;ctx.beginPath();ctx.arc(sx,sy,sr*4,0,Math.PI*2);ctx.fill();
    ctx.fillStyle='rgba(255,255,255,'+sa+')';
    ctx.beginPath();ctx.arc(sx,sy,sr,0,Math.PI*2);ctx.fill();
  }

  // ============================
  // 11. GROUND DAPPLED LIGHT
  // ============================
  ctx.globalCompositeOperation='screen';
  for(const[gx,gy,gr,ga]of[[CX-60,S*.82,35,.08],[CX+70,S*.88,30,.06],[CX,S*.92,26,.05]]){
    const gg=ctx.createRadialGradient(gx,gy,0,gx,gy,gr);
    gg.addColorStop(0,'rgba(255,255,220,'+ga+')');gg.addColorStop(1,'rgba(255,255,200,0)');
    ctx.fillStyle=gg;ctx.beginPath();ctx.arc(gx,gy,gr,0,Math.PI*2);ctx.fill();
  }
  ctx.globalCompositeOperation='source-over';

  // Pop clip before pixel processing
  ctx.restore();

  // ================================================
  // 12. PIXEL-LEVEL POST-PROCESSING (photorealistic)
  // ================================================
  const imgData=ctx.getImageData(0,0,S,S);
  const px=imgData.data;
  for(let y=0;y<S;y++){
    for(let x=0;x<S;x++){
      const i=(y*S+x)*4;
      if(px[i+3]<10) continue; // skip transparent (outside rounded rect)
      const bright=(px[i]+px[i+1]+px[i+2])/3;
      // (a) Perlin noise texture  Estronger in dark canopy areas
      const nStr=bright<80?.10:(bright<160?.05:.025);
      const nv1=fbm(n3,x*.008,y*.008,4)*nStr;
      const nv2=fbm(n1,x*.022,y*.022,3)*nStr*.4;
      const mod=1+nv1+nv2;
      // (b) Warm/cool color temperature shift
      const tShift=fbm(n2,x*.005,y*.005,3)*3;
      px[i]  =clamp(px[i]*mod+tShift);       // R warmer
      px[i+1]=clamp(px[i+1]*mod);             // G
      px[i+2]=clamp(px[i+2]*mod-tShift*.4);   // B cooler
      // (c) Film grain
      const grain=(Math.random()-.5)*5;
      px[i]=clamp(px[i]+grain);px[i+1]=clamp(px[i+1]+grain);px[i+2]=clamp(px[i+2]+grain);
      // (d) Vignette
      const dx=(x-CX)/(S*.5),dy=(y-CY)/(S*.5);
      const vig=1-Math.pow(Math.min(Math.sqrt(dx*dx+dy*dy)/1.3,1),2.5)*.3;
      px[i]=clamp(px[i]*vig);px[i+1]=clamp(px[i+1]*vig);px[i+2]=clamp(px[i+2]*vig);
    }
  }
  ctx.putImageData(imgData,0,0);

  // ============================
  // 13. RIM
  // ============================
  ctx.save();rrect(ctx,PAD,PAD,SZ,SZ,CR);
  const rim=ctx.createLinearGradient(0,PAD,0,S-PAD);
  rim.addColorStop(0,'rgba(255,255,255,.1)');rim.addColorStop(.5,'rgba(255,255,255,0)');
  rim.addColorStop(1,'rgba(0,0,0,.12)');
  ctx.strokeStyle=rim;ctx.lineWidth=1.5;ctx.stroke();ctx.restore();

  // ============================
  // AUTO-DOWNLOAD
  // ============================
  cv.toBlob(blob=>{
    const a=document.createElement('a');a.href=URL.createObjectURL(blob);
    a.download='komorebi-icon-1024.png';a.click();URL.revokeObjectURL(a.href);
    document.getElementById('st').textContent='Downloaded komorebi-icon-1024.png';
  },'image/png');
}

render();
</script>
</body>
</html>'''


def create_ico(images: dict[int, Image.Image], output_path: Path):
    """Create a .ico file with multiple resolutions (PNG-compressed)."""
    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    entries = []

    for size in ico_sizes:
        img = images[size]
        buf = io.BytesIO()
        img.save(buf, format='PNG')
        png_data = buf.getvalue()
        entries.append((size, png_data))

    num_images = len(entries)
    header = struct.pack('<HHH', 0, 1, num_images)
    dir_size = 16 * num_images
    offset = 6 + dir_size
    dir_entries = b''
    image_data = b''

    for size, png_data in entries:
        w = 0 if size >= 256 else size
        h = 0 if size >= 256 else size
        dir_entries += struct.pack(
            '<BBBBHHII',
            w, h, 0, 0, 1, 32,
            len(png_data), offset,
        )
        image_data += png_data
        offset += len(png_data)

    with open(output_path, 'wb') as f:
        f.write(header + dir_entries + image_data)


def create_icns(images: dict[int, Image.Image], output_path: Path):
    """Create a .icns file for macOS."""
    icns_types = [
        (b'ic07', 128),
        (b'ic08', 256),
        (b'ic09', 512),
        (b'ic10', 1024),
        (b'ic11', 32),
        (b'ic12', 64),
        (b'ic13', 256),
        (b'ic14', 512),
    ]
    entries = []
    for icon_type, size in icns_types:
        img = images.get(size)
        if img is None:
            continue
        buf = io.BytesIO()
        img.save(buf, format='PNG')
        png_data = buf.getvalue()
        entry_header = icon_type + struct.pack('>I', len(png_data) + 8)
        entries.append(entry_header + png_data)

    body = b''.join(entries)
    total_size = len(body) + 8
    header = b'icns' + struct.pack('>I', total_size)

    with open(output_path, 'wb') as f:
        f.write(header + body)


def cmd_svg():
    """Generate Canvas-based HTML preview for the icon."""
    html_str = create_canvas_html()

    html_path = PROJECT_ROOT / 'build' / 'resources' / 'app' / 'icon-preview.html'
    html_path.write_text(html_str, encoding='utf-8')
    print(f"  [OK] HTML preview: {html_path}")
    print(f"\n  Open the HTML in a browser to auto-download the 1024x1024 PNG.")
    return html_path


def cmd_pack(png_path: str):
    """Pack a 1024x1024 PNG into .ico, .icns, and project PNGs."""
    src = Path(png_path)
    if not src.exists():
        print(f"  File not found: {src}")
        sys.exit(1)

    master = Image.open(src).convert('RGBA')
    print(f"  [OK] Source: {master.size[0]}x{master.size[1]} PNG")

    all_sizes = [16, 24, 32, 48, 64, 128, 256, 512, 1024]
    images: dict[int, Image.Image] = {}
    for size in all_sizes:
        images[size] = master.resize((size, size), Image.LANCZOS)

    # Windows .ico
    ico_path = PROJECT_ROOT / 'src' / 'App.ico'
    create_ico(images, ico_path)
    print(f"  [OK] ICO: {ico_path}")

    # Linux PNG
    png_out = PROJECT_ROOT / 'build' / 'resources' / '_common' / 'icons' / 'komorebi.png'
    images[256].save(png_out, format='PNG')
    print(f"  [OK] PNG: {png_out}")

    # macOS .icns
    icns_path = PROJECT_ROOT / 'build' / 'resources' / 'app' / 'App.icns'
    create_icns(images, icns_path)
    print(f"  [OK] ICNS: {icns_path}")

    print("\n  Done! All icon formats generated.")


def main():
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python generate-icon.py svg              -> Create Canvas HTML preview")
        print("  python generate-icon.py pack <png_path>   -> Pack PNG into .ico/.icns")
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == 'svg':
        cmd_svg()
    elif cmd == 'pack':
        if len(sys.argv) < 3:
            print("  Provide path to 1024x1024 PNG")
            sys.exit(1)
        cmd_pack(sys.argv[2])
    else:
        print(f"  Unknown command: {cmd}")
        sys.exit(1)


if __name__ == '__main__':
    main()
