export class Result implements IResult, IMoreResult, IAnotherResult, IUnifiedResult {
// Properties from IResult.
val: number;
// Properties from IMoreResult.
moreVal: number;
// Properties from IAnotherResult.
anotherVal: number;
// Properties from IUnifiedResult.
}
export interface IResult {
val: number;
}
export interface IMoreResult extends IResult {
moreVal: number;
}
export interface IAnotherResult extends IResult {
anotherVal: number;
}
export interface IUnifiedResult extends IMoreResult, IResult, IAnotherResult {
}
