## about mvc filter



### 1. about



### 2. details

#### 2.1 filter 抽象

##### 2.1.1 filter metadata 接口

###### 2.1.1.2 filter container 接口

```c#
public interface IFilterContainer
{    
    IFilterMetadata FilterDefinition { get; set; }
}

```

