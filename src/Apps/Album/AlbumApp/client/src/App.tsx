import React, { Component } from 'react';
import { Route } from 'react-router';
import { Layout } from './components/Layout';
import { Home } from './components/Home';
import './App.css';

export default class App extends Component {
  static displayName = App.name;

  constructor(props:any) {
    super(props)
    let acckey = this.getQueryVariable("AccKey");
    if(acckey){
      this.setCookie('AccKey',acckey);
    }
  }

  setCookie(name: string, value: string, days: number | void) {
    var expires = "";
    if (days) {
      var date = new Date();
      date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
      expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/";
  }
  getCookie(name: string) {
    var nameEQ = name + "=";
    var ca = document.cookie.split(';');
    for (var i = 0; i < ca.length; i++) {
      var c = ca[i];
      while (c.charAt(0) === ' ') c = c.substring(1, c.length);
      if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
  }
  eraseCookie(name: string) {
    document.cookie = name + '=; Max-Age=-99999999;';
  }


  getQueryVariable(variable: string) {
    var query = window.location.search.substring(1);
    //console.log(query)//"app=article&act=news_content&aid=160990"
    var vars = query.split("&");
    //console.log(vars) //[ 'app=article', 'act=news_content', 'aid=160990' ]
    for (var i = 0; i < vars.length; i++) {
      var pair = vars[i].split("=");
      //console.log(pair)//[ 'app', 'article' ][ 'act', 'news_content' ][ 'aid', '160990' ] 
      if (pair[0] === variable) { return pair[1]; }
    }
    return null;
  }

  componentDidMount(){
    let acckey = this.getQueryVariable("AccKey");
    if(acckey){
      this.setCookie('AccKey',acckey);
    }
  }

  render() {

    return (
      <Layout>
        <Route exact path='/' component={Home} />
      </Layout>
    );
  }
}

