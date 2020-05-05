import React, { ReactNode } from 'react';
import 'rc-collapse/assets/index.css';
// @ts-ignore
import Collapse, { Panel } from 'rc-collapse';
import Gallery, { PhotoProps, RenderImageProps } from 'react-photo-gallery';
import "lightgallery.js/dist/css/lightgallery.css";
import './Home.css';




interface ImageInfo {
  Id: number,
  Path: string,
  Width: number,
  Height: number,
  IsVideo: number,
  IsWebImage:number
}
interface PhotoForGallery {
  src: string,
  width: number,
  height: number
  title: string,
}

interface IMediaBoxProps {
  shouldShow: boolean
  Year: string,
  Month: string,
  Day: string,
}
interface IMediaBoxState {
  photos: PhotoProps<{}>[],
  currentImage: number,
  viewerIsOpen: boolean
}
export class MediaBox extends React.Component<IMediaBoxProps, IMediaBoxState> {
  data: ImageInfo[]
  constructor(props: any) {
    super(props);
    this.state = {
      photos: [],
      currentImage: 0,
      viewerIsOpen: false
    };
    this.data = [];
    this.openLightbox = this.openLightbox.bind(this);
    this.closeLightbox = this.closeLightbox.bind(this);
    this.renderImage = this.renderImage.bind(this);
  }
  componentDidMount() {
    this.populateImageData();
  }

  async populateImageData() {
    let params: { [key: string]: string } = {
      y: this.props.Year,
      m: this.props.Month,
      d: this.props.Day,
    };

    let query = Object.keys(params)
      .map((k: string) => encodeURIComponent(k) + '=' + encodeURIComponent(params[k]))
      .join('&');
    let url = "/api/Apps/Album/GetMediaInDay?" + query;

    
    let response = await fetch(url);
    this.data = await response.json();
    this.data.sort(function (a: ImageInfo, b: ImageInfo) { return a.Path.localeCompare(b.Path); });
    let ps = this.data.map((x: ImageInfo) => {
      if (x.Width === 0) {
        x.Width = 1;
      }
      if (x.Height === 0) {
        x.Height = 1;
      }
      return {
        src: "/api/Apps/Album/GetMediaThumbnail?id=" + x.Id,
        title: x.Path,
        width: x.Width,
        height: x.Height,
      };
    });
    this.setState({ photos: ps });
  }
  openLightbox(event: any, objarg: { photo: PhotoProps<{}>, index: number }) {
    let tmp: any = window;
    let nod = document.getElementById("h5v" + this.props.Year + this.props.Month + this.props.Day);
    if (nod === null) {
      return;
    }
    let ds = this.data.map(x => {
      if (x.IsVideo > 0) {
        return {
          html: "#vid" + x.Id,
          poster:"/api/Apps/Album/GetMediaThumbnail?id=" + x.Id,
          subHtml: x.Path,
          downloadUrl: "/api/Apps/Album/DownloadMedia?id=" + x.Id,
        };
      } else {
        let rp = "/api/Apps/Album/GetMediaThumbnail?id=" + x.Id;
        if(x.IsWebImage>0){
          rp = "/api/Apps/Album/GetMedia?id=" + x.Id;
        }
        return {
          src: rp ,
          thumb: "/api/Apps/Album/GetMediaThumbnail?id=" + x.Id,
          subHtml: x.Path,
          downloadUrl: "/api/Apps/Album/DownloadMedia?id=" + x.Id,
        };
      }
    });
    let lgd = nod.getAttribute('lg-uid');
    if (lgd !== null) {
      window.lgData[lgd].s.index = objarg.index;
    }
    console.log(objarg.index);
    console.log(nod);
    tmp.lightGallery(nod, {
      enableTouch: true,
      swipeThreshold: 30,
      dynamic: true,
      index: objarg.index,
      dynamicEl: ds
    });
    console.log(nod);

  }
  closeLightbox() {
    this.setState({ currentImage: 0, viewerIsOpen: false });
  }
  renderImage(imginf: RenderImageProps<{}>) {
    let imgStyle = { margin: imginf.margin};
    const handleClick = (event: React.MouseEvent) => {
      if (imginf === null) {
        return
      }
      if (imginf.onClick === null) {
        return
      }
      let arg: { photo: PhotoProps<{}>, index: number } = { photo: imginf.photo, index: imginf.index };
      imginf.onClick(event, arg);
    };
    let hidestyle = { display: "none" }; 
    let newimginfo: ImageInfo = this.data[imginf.index];
    if (newimginfo.IsVideo > 0) {
      return (<div className={"vimg"}>
        <img key={imginf.photo.key} alt={imginf.photo.alt} className={"vimg"} style={imgStyle}
        src={imginf.photo.src} width={imginf.photo.width} height={imginf.photo.height}
        onClick={handleClick} />
        <img className={"playbtn"} alt="" src={require('./playbtn.png')} onClick={handleClick}/>
        <div style={hidestyle} id={"vid" + newimginfo.Id}>
          <video className="lg-video-object lg-html5" controls preload="none">
            <source src={"/api/Apps/Album/GetMedia?id=" + newimginfo.Id} type="video/mp4" />
               Your browser does not support HTML5 video.
             </video>
        </div>
      </div>
      );
    } else {
      return (<img key={imginf.photo.key} alt={imginf.photo.alt} style={imgStyle}
        src={imginf.photo.src} width={imginf.photo.width} height={imginf.photo.height}
        onClick={handleClick}
      />);
    }
  }
  render() {
    return (
      <div id={"h5v" + this.props.Year + this.props.Month + this.props.Day}>
        <Gallery targetRowHeight={192} renderImage={this.renderImage} photos={this.state.photos} onClick={this.openLightbox} />
      </div>
    );
  }
}


interface IHomeDayProps {
  Year: string,
  Month: string,
  Days: [],
  InitExpanded: boolean
}
interface IHomeDayState {
}
export class HomeDay extends React.Component<IHomeDayProps, IHomeDayState> {

  Shows: object
  constructor(props: any) {
    super(props);
    this.Shows = {};
    this.onChange = this.onChange.bind(this);
  }

  onChange = (activeKey: string) => {
    this.Shows = {};
    console.log(activeKey);
  }
  render() {
    let daysitems: ReactNode[] = this.props.Days.map((x) => {
      let date: Date = new Date(Number(this.props.Year), Number(this.props.Month) - 1, Number(x));
      let key = this.props.Year + '.' + this.props.Month + '.' + x;
      return <Panel header={date.toDateString()} key={key} >
        <MediaBox Year={this.props.Year} Month={this.props.Month} Day={x} shouldShow={this.props.InitExpanded} />
      </Panel>
    });
    let keys = this.props.Days.map((x) => this.props.Year + '.' + this.props.Month + '.' + x);
    return <Collapse onChange={this.onChange} defaultActiveKey={keys} >
      {daysitems}
    </Collapse>
  }
}



interface IHomeMonthProps {
  Year: string,
  Months: object,
  ExpandedFirst: boolean
}
interface IHomeMonthState {
}
export class HomeMonth extends React.Component<IHomeMonthProps, IHomeMonthState> {
  ExpandedFirst: boolean;
  constructor(props: any) {
    super(props);
    this.ExpandedFirst = false;
  }

  render() {
    this.ExpandedFirst = this.props.ExpandedFirst;
    let mon: any = this.props.Months;
    let monitems: ReactNode[] = [];
    let monkeys = Object.keys(mon);
    monkeys.sort(function (a, b) { return Number(b) - Number(a); });
    for (let moncurprop of monkeys) {
      let date: Date = new Date(Number(this.props.Year), Number(moncurprop) - 1, 3);
      monitems.push(<Panel header={date.toLocaleString('default', { month: 'long' })} key={this.props.Year + moncurprop} >
        <HomeDay InitExpanded={this.ExpandedFirst} Year={this.props.Year} Month={moncurprop} Days={mon[moncurprop]} />
      </Panel>
      );
      this.ExpandedFirst = false;
    }
    if (this.props.ExpandedFirst) {
      return <Collapse defaultActiveKey={this.props.Year + monkeys[0]}>
        {monitems}
      </Collapse>
    } else {
      return <Collapse>
        {monitems}
      </Collapse>
    }
  }
}


declare global {
  interface Window {
    lgData: any;
    lightGallery: any
  }
}


interface IProps {
}

interface IState {
  photos: []
  days: any
  loading: boolean
  activeKey: string
}

export class Home extends React.Component<IProps, IState> {
  static displayName = Home.name;

  constructor(props: IProps) {
    super(props);
    this.state = { photos: [], days: [], loading: false, activeKey: "1" };
  }
  componentDidMount() {
    this.populateDays();
    if (!window.lgData) {
      import("lightgallery.js").then(() => {
        import("lg-autoplay.js").then();
        import("lg-fullscreen.js").then();
        import("lg-pager.js").then();
        import("lg-video.js").then();
        import("lg-zoom.js").then();
      });
    }
  }

  async populateDays() {
    let response = await fetch('/api/Apps/Album/GetDays');
    let data = await response.json();
    let key: string = "";
    for (let curprop in data) {
      key = curprop;
      break;
    }
    this.setState({ days: data, loading: false, activeKey: key });
  }


  getItems() {
    if (this.state.days === undefined) {
      return "Loading...";
    } else {
      let items: ReactNode[] = [];
      let yeatkeys = Object.keys(this.state.days);
      if (yeatkeys.length < 1) {
        return "No Album";
      }
      yeatkeys.sort(function (a, b) { return Number(b) - Number(a); });
      for (let yearprop of yeatkeys) {
        items.push(<Panel header={yearprop} key={yearprop} >
          <HomeMonth ExpandedFirst={yearprop === yeatkeys[0]} Year={yearprop} Months={this.state.days[yearprop]} />
        </Panel>
        );
      }
      return <Collapse defaultActiveKey={yeatkeys[0]}>
        {items}
      </Collapse>;
    }

  }

  render() {
    return (this.getItems());
  }
}
